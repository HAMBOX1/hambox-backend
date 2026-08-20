using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;

/// <summary>
/// A Bamboo response Bamboo itself gave a definite (non-ambiguous) negative answer to — a parsed 400
/// with a reason code, or a 401/403/429. <see cref="BambooSupplierProvider"/> maps
/// <see cref="ReasonCode"/>/<see cref="HttpStatusCode"/> into the generic
/// <c>SupplierFulfillmentFailureCategory</c>; this type carries only the raw facts.
/// </summary>
/// <remarks>
/// The exception's own <see cref="Exception.Message"/> prefers <paramref name="rawMessage"/> (the
/// documented "message" field) but falls back to <paramref name="reasonCode"/> when only that is
/// present — confirmed necessary against the real sandbox: its actual 403 body carries only an
/// <c>"error"</c> field (captured into <paramref name="reasonCode"/> via <see cref="BambooErrorResponseBody.Error"/>),
/// not a <c>"message"</c> field, so relying on <paramref name="rawMessage"/> alone silently discarded a
/// real, safe, useful diagnostic string in favor of a generic "HTTP 403" fallback.
/// </remarks>
internal sealed class BambooApiException(int httpStatusCode, string? reasonCode, string? rawMessage)
    : Exception(rawMessage ?? reasonCode ?? $"Bamboo API returned HTTP {httpStatusCode}.")
{
    public int HttpStatusCode { get; } = httpStatusCode;

    public string? ReasonCode { get; } = reasonCode;
}

/// <summary>
/// The outcome could not be determined with confidence — timeout, connection failure, 5xx, an
/// unparsable/unexpected response shape, or Bamboo's own <c>OrderAlreadyExists</c> (which means the
/// provider already has this reference but does not tell us, in this response, what its outcome was).
/// Callers must never treat this as failure — only <c>GetOrderStatusAsync</c> can resolve it.
/// </summary>
internal sealed class BambooAmbiguousResponseException(string reason, Exception? inner = null) : Exception(reason, inner);

/// <summary>
/// Thin, typed wrapper over the three Bamboo endpoints this integration uses. Holds no state about
/// which <c>Supplier</c> it's acting for — credentials are passed in per call (never cached, never
/// defaulted onto the shared <see cref="HttpClient"/>), matching the existing
/// <c>DotPaymentGateway</c> typed-HttpClient pattern in this codebase.
/// </summary>
internal sealed class BambooHttpClient(HttpClient httpClient, IOptions<BambooProviderOptions> options, ILogger<BambooHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BambooAccountsResponseBody> GetAccountsAsync(
        SupplierProviderCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Get, BambooProviderConstants.AccountsPath, credentials);
        return await SendAsync<BambooAccountsResponseBody>(request, cancellationToken);
    }

    /// <summary>
    /// Submits a purchase. Returns the raw <c>requestId</c> Bamboo returned when it accepts (200) —
    /// per the documentation this only confirms acceptance, not delivery; the caller must reconcile via
    /// <see cref="GetOrderAsync"/>. Throws <see cref="BambooApiException"/> for a definite rejection,
    /// <see cref="BambooAmbiguousResponseException"/> for anything else including
    /// <c>OrderAlreadyExists</c>.
    /// </summary>
    public async Task<string?> PlaceOrderAsync(
        SupplierProviderCredentials credentials,
        Guid requestId,
        long accountId,
        long productId,
        int quantity,
        decimal value,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Post, BambooProviderConstants.PlaceOrderPath, credentials);
        var body = new BambooCheckoutRequestBody(requestId, accountId, [new BambooCheckoutProduct(productId, quantity, value)]);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        // Confirmed against the real sandbox: a successful Place Order's body is a bare JSON string
        // (e.g. "c33efecf-...") — NOT an object with a "requestId" property, despite the documentation
        // prose implying an object. Deserializing as string here (not BambooCheckoutResponseBody) is
        // what makes acceptance actually parse instead of always falling into "unparsable body".
        var returnedRequestId = await SendAsync<string>(request, cancellationToken);

        // Still never trust a blank/empty body as acceptance — resolve via reconciliation instead of guessing.
        if (string.IsNullOrWhiteSpace(returnedRequestId))
        {
            throw new BambooAmbiguousResponseException("Bamboo returned 200 with an empty requestId — cannot confirm acceptance.");
        }

        return returnedRequestId;
    }

    /// <summary>Browses the supplier's live catalog (Get Catalog) for admin product-mapping selection — never triggered by anything except an explicit, debounced admin search.</summary>
    public async Task<BambooCatalogResponseBody> GetCatalogAsync(
        SupplierProviderCredentials credentials, string? searchTerm, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"PageIndex={pageIndex}", $"PageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query.Add($"Name={Uri.EscapeDataString(searchTerm)}");
        }

        var path = $"{BambooProviderConstants.CatalogPath}?{string.Join('&', query)}";
        using var request = BuildRequest(HttpMethod.Get, path, credentials);
        return await SendAsync<BambooCatalogResponseBody>(request, cancellationToken);
    }

    public async Task<BambooOrderResponseBody> GetOrderAsync(
        SupplierProviderCredentials credentials, string requestId, CancellationToken cancellationToken)
    {
        var path = string.Format(BambooProviderConstants.GetOrderPathFormat, Uri.EscapeDataString(requestId));
        using var request = BuildRequest(HttpMethod.Get, path, credentials);
        return await SendAsync<BambooOrderResponseBody>(request, cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, SupplierProviderCredentials credentials)
    {
        var request = new HttpRequestMessage(method, path);

        // Basic Auth per the documented mechanism, built fresh per call — never cached on the shared
        // HttpClient's default headers, so no risk of one supplier's credentials leaking onto another
        // supplier's request if multiple Bamboo Supplier rows ever share this same typed client.
        if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
        {
            throw new BambooApiException(0, null, "Bamboo credentials are not configured for this supplier.");
        }

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ApiKey}:{credentials.ApiSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
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
            // Our own timeout fired, not the caller's cancellation — this is exactly "we do not know
            // whether Bamboo received/processed the request."
            throw new BambooAmbiguousResponseException("Bamboo request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new BambooAmbiguousResponseException("Bamboo request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > options.Value.MaxResponseBytes)
            {
                throw new BambooAmbiguousResponseException(
                    $"Bamboo response declared {declaredLength} bytes, exceeding the configured {options.Value.MaxResponseBytes}-byte limit — refusing to read it.");
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
                throw new BambooAmbiguousResponseException("Bamboo response exceeded the configured maximum size — refusing to read it.");
            }

            // Never log the raw body — it may contain redemption codes/PINs. Only safe, structural facts.
            logger.LogDebug("Bamboo {Method} {Path} responded {StatusCode}.", request.Method, request.RequestUri, (int)response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TResponse>(raw, JsonOptions);
                    return parsed ?? throw new BambooAmbiguousResponseException("Bamboo returned an empty/null success body.");
                }
                catch (JsonException ex)
                {
                    throw new BambooAmbiguousResponseException("Bamboo returned a success status with an unparsable body.", ex);
                }
            }

            HandleErrorResponse(response.StatusCode, raw);
            throw new InvalidOperationException("Unreachable — HandleErrorResponse always throws.");
        }
    }

    private static void HandleErrorResponse(HttpStatusCode statusCode, string raw)
    {
        BambooErrorResponseBody? error = null;
        try
        {
            error = JsonSerializer.Deserialize<BambooErrorResponseBody>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            // Undocumented error envelope shape — parse defensively, fall through to status-code-only handling.
        }

        // Confirmed against the real sandbox: Bamboo's actual Place Order 400 body for a duplicate
        // RequestId is `{"message":"OrderAlreadyExists"}` — the reason code travels in "message", not
        // a dedicated "reasonCode"/"code"/"error" field as the documentation prose implied. Checking
        // "message" as a reason-code candidate too is what makes the OrderAlreadyExists match below
        // actually fire against real responses — without it, the single most safety-critical mapping in
        // this integration silently never matched, misclassifying a safe/ambiguous outcome as a
        // definite UnknownProviderState failure.
        var reasonCode = error?.ReasonCode ?? error?.Code ?? error?.Error ?? error?.Message;
        var message = error?.Message ?? error?.Error;

        // message/reasonCode are passed through as-is below — BambooApiException itself falls back
        // rawMessage -> reasonCode -> a generic "HTTP {status}" string, so a call site only needs to
        // supply an explicit override when neither field Bamboo actually sent would be informative.
        switch ((int)statusCode)
        {
            case 401:
            case 403:
                throw new BambooApiException((int)statusCode, reasonCode, message);

            case 429:
                // A definite, known outcome (the request was rejected before processing) — not ambiguous.
                throw new BambooApiException(429, reasonCode, message);

            case 400 when string.Equals(reasonCode, BambooReasonCode.OrderAlreadyExists, StringComparison.OrdinalIgnoreCase):
                // Bamboo already has this RequestId — we do not know its outcome from this response
                // alone. This is exactly the safe path: never treat as failure, resolve via GetOrder.
                throw new BambooAmbiguousResponseException("Bamboo reported OrderAlreadyExists for this reference — resolve via GetOrder.");

            case 400 when reasonCode is not null:
                throw new BambooApiException(400, reasonCode, message);

            case >= 500:
                throw new BambooAmbiguousResponseException($"Bamboo returned HTTP {(int)statusCode} — outcome unknown.");

            default:
                // Any other status, or a 400 whose body didn't parse into a recognizable reason code —
                // never guess a category for something undocumented.
                throw new BambooApiException((int)statusCode, reasonCode, message);
        }
    }
}

file sealed class BoundedStreamLimitExceededException : Exception;

/// <summary>Wraps a stream and throws rather than silently truncating once <paramref name="maxBytes"/> is exceeded — a defensive backstop alongside <c>HttpClient.MaxResponseContentBufferSize</c>.</summary>
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
