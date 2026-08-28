using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;

/// <summary>
/// A GlobeTopper response GlobeTopper itself gave a definite (non-ambiguous) negative answer to — a real
/// 401/403/429, or (for the Purchase endpoint only) a parsed HTTP-200 body whose <c>responseCode</c> is a
/// documented non-success value. <see cref="GlobeTopperSupplierProvider"/> maps this into the generic
/// <c>SupplierFulfillmentFailureCategory</c>; this type carries only the raw facts.
/// </summary>
internal sealed class GlobeTopperApiException(int httpStatusCode, int? responseCode, string? rawMessage)
    : Exception(rawMessage ?? $"GlobeTopper API returned HTTP {httpStatusCode} (responseCode {responseCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}).")
{
    public int HttpStatusCode { get; } = httpStatusCode;

    public int? ResponseCode { get; } = responseCode;
}

/// <summary>
/// The outcome could not be determined with confidence — timeout, connection failure, a real 5xx, or an
/// unparsable/unexpected response shape. Callers must never treat this as failure — only
/// <see cref="GlobeTopperSupplierProvider.GetOrderStatusAsync"/> can resolve it, and — a genuine,
/// documented limitation of this API — it can only do that when a <c>trans_id</c> was already captured;
/// see that method's remarks.
/// </summary>
internal sealed class GlobeTopperAmbiguousResponseException(string reason, Exception? inner = null) : Exception(reason, inner);

/// <summary>
/// Thin, typed wrapper over the GlobeTopper endpoints this integration uses. Holds no state about which
/// <c>Supplier</c> it's acting for — credentials are passed in per call (never cached, never defaulted
/// onto the shared <see cref="HttpClient"/>), matching <c>BambooHttpClient</c>/<c>VisoriaHttpClient</c>'s
/// identical pattern.
/// </summary>
internal sealed class GlobeTopperHttpClient(HttpClient httpClient, IOptions<GlobeTopperProviderOptions> options, ILogger<GlobeTopperHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GlobeTopperUser?> GetUserAsync(SupplierProviderCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Get, GlobeTopperProviderConstants.UserPath, credentials);
        var envelope = await SendAsync<GlobeTopperEnvelope<GlobeTopperUser>>(request, cancellationToken);
        return envelope.Records?.FirstOrDefault();
    }

    /// <summary>
    /// No pagination and no free-text search parameter exist on this endpoint (confirmed against the
    /// live OpenAPI document and a real sandbox call returning every product in one response) — callers
    /// filter/page client-side.
    /// </summary>
    public async Task<IReadOnlyList<GlobeTopperProduct>> SearchProductsAsync(SupplierProviderCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Get, GlobeTopperProviderConstants.ProductsPath, credentials);
        var envelope = await SendAsync<GlobeTopperEnvelope<GlobeTopperProduct>>(request, cancellationToken);
        return envelope.Records ?? [];
    }

    /// <summary>
    /// Submits a purchase. Unlike every other call in this client, a non-success business outcome is
    /// still delivered as HTTP 200 with a non-<c>200</c> <see cref="GlobeTopperEnvelope{TRecord}.ResponseCode"/>
    /// (confirmed: the documentation's own out-of-stock/insufficient-balance/access-denied examples are
    /// all listed under one HTTP 200 response) — so this returns the raw envelope rather than throwing on
    /// a business failure, and the caller (<see cref="GlobeTopperSupplierProvider"/>) is responsible for
    /// inspecting <see cref="GlobeTopperEnvelope{TRecord}.ResponseCode"/>. A genuine ambiguous outcome
    /// (timeout/5xx/malformed body/an unexpected non-200 HTTP status such as a real 401/403/429) still
    /// throws exactly like every other method here.
    /// </summary>
    public async Task<GlobeTopperEnvelope<GlobeTopperTransaction>> PurchaseAsync(
        SupplierProviderCredentials credentials,
        long productId,
        decimal amount,
        long orderId,
        CancellationToken cancellationToken)
    {
        var path = string.Format(
            CultureInfo.InvariantCulture,
            GlobeTopperProviderConstants.PurchasePathFormat,
            productId,
            amount.ToString(CultureInfo.InvariantCulture));

        using var request = BuildRequest(HttpMethod.Post, path, credentials);

        var form = new Dictionary<string, string>
        {
            ["email"] = options.Value.PurchaserEmail,
            ["first_name"] = options.Value.PurchaserFirstName,
            ["last_name"] = options.Value.PurchaserLastName,
            ["order_id"] = orderId.ToString(CultureInfo.InvariantCulture),
        };
        request.Content = new FormUrlEncodedContent(form);

        return await SendAsync<GlobeTopperEnvelope<GlobeTopperTransaction>>(request, cancellationToken);
    }

    /// <summary>Looks a transaction up by GlobeTopper's own <c>trans_id</c> — there is no documented way to look one up by any client-supplied reference; see <c>GlobeTopperSupplierProvider.GetOrderStatusAsync</c>'s remarks.</summary>
    public async Task<GlobeTopperTransaction?> GetTransactionAsync(SupplierProviderCredentials credentials, string transactionId, CancellationToken cancellationToken)
    {
        var path = string.Format(GlobeTopperProviderConstants.TransactionByIdPathFormat, Uri.EscapeDataString(transactionId));
        using var request = BuildRequest(HttpMethod.Get, path, credentials);
        var envelope = await SendAsync<GlobeTopperEnvelope<GlobeTopperTransaction>>(request, cancellationToken);
        return envelope.Records?.FirstOrDefault();
    }

    /// <summary>
    /// Bearer auth per the documented mechanism (<c>Authorization: Bearer {{api_key}}:{{api_token}}</c>),
    /// built fresh per call — never cached on the shared <see cref="HttpClient"/>'s default headers, so no
    /// risk of one supplier's credentials leaking onto another supplier's request if multiple GlobeTopper
    /// <c>Supplier</c> rows ever share this same typed client. GlobeTopper's key/secret pair is stored in
    /// the existing <c>ApiKey</c>/<c>ApiSecret</c> credential fields (not <c>Username</c>/<c>BearerToken</c>)
    /// — see <c>GlobeTopperSupplierProvider</c>'s remarks for why.
    /// </summary>
    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, SupplierProviderCredentials credentials)
    {
        var request = new HttpRequestMessage(method, path);

        if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
        {
            throw new GlobeTopperApiException(0, null, "GlobeTopper credentials are not configured for this supplier.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{credentials.ApiKey}:{credentials.ApiSecret}");
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
            // GlobeTopper received/processed the request."
            throw new GlobeTopperAmbiguousResponseException("GlobeTopper request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GlobeTopperAmbiguousResponseException("GlobeTopper request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > options.Value.MaxResponseBytes)
            {
                throw new GlobeTopperAmbiguousResponseException(
                    $"GlobeTopper response declared {declaredLength} bytes, exceeding the configured {options.Value.MaxResponseBytes}-byte limit — refusing to read it.");
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
                throw new GlobeTopperAmbiguousResponseException("GlobeTopper response exceeded the configured maximum size — refusing to read it.");
            }

            // Never log the raw body — it may contain redemption codes/PINs (e.g. Purchase's extra_fields)
            // or account/PII data (e.g. GET /user). Only safe, structural facts.
            logger.LogDebug("GlobeTopper {Method} {Path} responded {StatusCode}.", request.Method, request.RequestUri, (int)response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TResponse>(raw, JsonOptions);
                    return parsed ?? throw new GlobeTopperAmbiguousResponseException("GlobeTopper returned an empty/null success body.");
                }
                catch (JsonException ex)
                {
                    throw new GlobeTopperAmbiguousResponseException("GlobeTopper returned a success status with an unparsable body.", ex);
                }
            }

            HandleErrorResponse(response.StatusCode);
            throw new InvalidOperationException("Unreachable — HandleErrorResponse always throws.");
        }
    }

    /// <summary>
    /// Real, non-200 HTTP statuses — never documented for any GlobeTopper endpoint (every documented
    /// outcome, success or business failure, arrives under HTTP 200; see
    /// <see cref="GlobeTopperEnvelope{TRecord}"/>'s remarks), but Bamboo's integration found a real,
    /// completely undocumented AWS-load-balancer-level 403 (IP allowlisting) before its application logic
    /// ever ran — this exists as the same defensive safety net, not because GlobeTopper documents it.
    /// </summary>
    private static void HandleErrorResponse(HttpStatusCode statusCode)
    {
        switch ((int)statusCode)
        {
            case 401:
            case 403:
                throw new GlobeTopperApiException((int)statusCode, null, null);

            case 429:
                throw new GlobeTopperApiException(429, null, null);

            case >= 500:
                throw new GlobeTopperAmbiguousResponseException($"GlobeTopper returned HTTP {(int)statusCode} — outcome unknown.");

            default:
                // Never guessed into a specific category beyond the raw facts.
                throw new GlobeTopperApiException((int)statusCode, null, null);
        }
    }
}

file sealed class BoundedStreamLimitExceededException : Exception;

/// <summary>Wraps a stream and throws rather than silently truncating once <paramref name="maxBytes"/> is exceeded — duplicated from <c>BambooHttpClient</c>/<c>VisoriaHttpClient</c>'s identical file-scoped helper deliberately; no shared base was introduced for one more provider.</summary>
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
