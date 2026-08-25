using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;

/// <summary>
/// A Visoria response Visoria itself gave a definite (non-ambiguous) negative answer to — 401/403/404/422/429.
/// <see cref="VisoriaSupplierProvider"/> maps <see cref="HttpStatusCode"/> into the generic
/// <c>SupplierFulfillmentFailureCategory</c>; this type carries only the raw facts.
/// </summary>
internal sealed class VisoriaApiException(int httpStatusCode, string? code, string? rawMessage)
    : Exception(rawMessage ?? code ?? $"Visoria API returned HTTP {httpStatusCode}.")
{
    public int HttpStatusCode { get; } = httpStatusCode;

    public string? Code { get; } = code;
}

/// <summary>
/// The outcome could not be determined with confidence — timeout, connection failure, 5xx, or an
/// unparsable/unexpected response shape. Callers must never treat this as failure — only
/// <c>GetOrderStatusAsync</c> can resolve it. Unlike Bamboo, Visoria's own idempotency-key reuse never
/// produces an ambiguous "already exists" error (a duplicate <c>Idempotency-Key</c> just returns the
/// existing order with a normal 200, per the documentation) — so no analogous special-case mapping is
/// needed here.
/// </summary>
internal sealed class VisoriaAmbiguousResponseException(string reason, Exception? inner = null) : Exception(reason, inner);

/// <summary>
/// Thin, typed wrapper over the Visoria endpoints this integration uses. Holds no state about which
/// <c>Supplier</c> it's acting for — credentials are passed in per call (never cached, never defaulted
/// onto the shared <see cref="HttpClient"/>), matching <c>BambooHttpClient</c>'s identical pattern.
/// </summary>
internal sealed class VisoriaHttpClient(HttpClient httpClient, IOptions<VisoriaProviderOptions> options, ILogger<VisoriaHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<VisoriaBalance>> GetBalanceAsync(SupplierProviderCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Get, VisoriaProviderConstants.BalancePath, credentials);
        return await SendAsync<IReadOnlyList<VisoriaBalance>>(request, cancellationToken);
    }

    /// <summary>No text-search query parameter exists on this endpoint (confirmed against the OpenAPI spec) — callers page through results and filter client-side.</summary>
    public async Task<VisoriaProductListResponse> GetProductsAsync(
        SupplierProviderCredentials credentials, int page, int limit, CancellationToken cancellationToken)
    {
        var path = $"{VisoriaProviderConstants.ProductsPath}?page={page}&limit={Math.Min(limit, VisoriaProviderConstants.MaxPageSize)}";
        using var request = BuildRequest(HttpMethod.Get, path, credentials);
        return await SendAsync<VisoriaProductListResponse>(request, cancellationToken);
    }

    public async Task<VisoriaProduct> GetProductAsync(SupplierProviderCredentials credentials, string productId, CancellationToken cancellationToken)
    {
        var path = string.Format(VisoriaProviderConstants.ProductPathFormat, Uri.EscapeDataString(productId));
        using var request = BuildRequest(HttpMethod.Get, path, credentials);
        return await SendAsync<VisoriaProduct>(request, cancellationToken);
    }

    /// <summary>
    /// Creates (or, on a reused <paramref name="idempotencyKey"/>, returns the existing) order.
    /// <paramref name="idempotencyKey"/> is sent as the documented <c>Idempotency-Key</c> header — the
    /// caller's <c>SupplierFulfillment.HamboxReferenceId</c>, per <see cref="ISupplierProvider.PurchaseAsync"/>'s
    /// contract.
    /// </summary>
    public async Task<VisoriaOrder> CreateOrderAsync(
        SupplierProviderCredentials credentials, string idempotencyKey, VisoriaCreateOrderRequestBody body, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Post, VisoriaProviderConstants.OrdersPath, credentials);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        return await SendAsync<VisoriaOrder>(request, cancellationToken);
    }

    public async Task<VisoriaOrder> GetOrderByIdempotencyKeyAsync(
        SupplierProviderCredentials credentials, string idempotencyKey, CancellationToken cancellationToken)
    {
        var path = string.Format(VisoriaProviderConstants.OrderByIdempotencyKeyPathFormat, Uri.EscapeDataString(idempotencyKey));
        using var request = BuildRequest(HttpMethod.Get, path, credentials);
        return await SendAsync<VisoriaOrder>(request, cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, SupplierProviderCredentials credentials)
    {
        var request = new HttpRequestMessage(method, path);

        // Bearer auth per the documented mechanism, built fresh per call — never cached on the shared
        // HttpClient's default headers, so no risk of one supplier's key leaking onto another
        // supplier's request if multiple Visoria Supplier rows ever share this same typed client.
        if (string.IsNullOrEmpty(credentials.BearerToken))
        {
            throw new VisoriaApiException(0, null, "Visoria credentials are not configured for this supplier.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.BearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own timeout fired, not the caller's cancellation — exactly "we do not know whether
            // Visoria received/processed the request."
            throw new VisoriaAmbiguousResponseException("Visoria request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new VisoriaAmbiguousResponseException("Visoria request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > options.Value.MaxResponseBytes)
            {
                throw new VisoriaAmbiguousResponseException(
                    $"Visoria response declared {declaredLength} bytes, exceeding the configured {options.Value.MaxResponseBytes}-byte limit — refusing to read it.");
            }

            string raw;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var bounded = new BoundedReadStream(stream, options.Value.MaxResponseBytes);
                using var reader = new StreamReader(bounded, Encoding.UTF8);
                raw = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (BoundedStreamLimitExceededException)
            {
                throw new VisoriaAmbiguousResponseException("Visoria response exceeded the configured maximum size — refusing to read it.");
            }

            // Never log the raw body — it may contain redemption codes/PINs. Only safe, structural facts.
            logger.LogDebug("Visoria {Method} {Path} responded {StatusCode}.", request.Method, request.RequestUri, (int)response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TResponse>(raw, JsonOptions);
                    return parsed ?? throw new VisoriaAmbiguousResponseException("Visoria returned an empty/null success body.");
                }
                catch (JsonException ex)
                {
                    throw new VisoriaAmbiguousResponseException("Visoria returned a success status with an unparsable body.", ex);
                }
            }

            HandleErrorResponse(response.StatusCode, raw);
            throw new InvalidOperationException("Unreachable — HandleErrorResponse always throws.");
        }
    }

    private static void HandleErrorResponse(HttpStatusCode statusCode, string raw)
    {
        VisoriaErrorBody? error = null;
        try
        {
            error = JsonSerializer.Deserialize<VisoriaErrorBody>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            // Malformed/unexpected error envelope — parse defensively, fall through to status-code-only handling.
        }

        switch ((int)statusCode)
        {
            case 401:
            case 403:
            case 404:
            case 422:
                // All definite, documented negative answers — the request was rejected before any
                // fulfillment was attempted, never an unknown/in-flight outcome.
                throw new VisoriaApiException((int)statusCode, error?.Code, error?.Message);

            case 429:
                throw new VisoriaApiException(429, error?.Code, error?.Message);

            case >= 500:
                throw new VisoriaAmbiguousResponseException($"Visoria returned HTTP {(int)statusCode} — outcome unknown.");

            default:
                // Any other status — never guessed into a specific category beyond the raw facts.
                throw new VisoriaApiException((int)statusCode, error?.Code, error?.Message);
        }
    }
}

file sealed class BoundedStreamLimitExceededException : Exception;

/// <summary>Wraps a stream and throws rather than silently truncating once <paramref name="maxBytes"/> is exceeded — a defensive backstop alongside <c>HttpClient.MaxResponseContentBufferSize</c>. Duplicated from <c>BambooHttpClient</c>'s identical file-scoped helper deliberately — Bamboo's file is left untouched, and no shared base was introduced for one extra provider.</summary>
file sealed class BoundedReadStream(Stream inner, int maxBytes) : Stream
{
    private int _readSoFar;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        _readSoFar += read;
        if (_readSoFar > maxBytes)
        {
            throw new BoundedStreamLimitExceededException();
        }

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        _readSoFar += read;
        if (_readSoFar > maxBytes)
        {
            throw new BoundedStreamLimitExceededException();
        }

        return read;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
