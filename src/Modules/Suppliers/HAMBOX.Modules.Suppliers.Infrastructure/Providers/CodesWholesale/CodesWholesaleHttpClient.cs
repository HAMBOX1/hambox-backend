using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;

/// <summary>
/// A response CodesWholesale itself gave a definite (non-ambiguous) negative answer to — a parsed 4xx
/// business error, an OAuth token rejection, or a 429. <see cref="CodesWholesaleSupplierProvider"/> maps
/// <see cref="ErrorCode"/>/<see cref="HttpStatusCode"/> into the generic <c>SupplierFulfillmentFailureCategory</c>;
/// this type carries only the raw facts.
/// </summary>
internal sealed class CodesWholesaleApiException(int httpStatusCode, int? errorCode, string? rawMessage)
    : Exception(rawMessage ?? $"CodesWholesale API returned HTTP {httpStatusCode}.")
{
    public int HttpStatusCode { get; } = httpStatusCode;

    public int? ErrorCode { get; } = errorCode;
}

/// <summary>
/// The outcome could not be determined with confidence — timeout, connection failure, a real 5xx, a 404
/// on an order lookup (documentation never states whether this means "never created" or "not yet
/// visible" — never guessed), or an unparsable/unexpected response shape. Callers must never treat this
/// as failure — only <see cref="CodesWholesaleSupplierProvider.GetOrderStatusAsync"/> can resolve it.
/// </summary>
internal sealed class CodesWholesaleAmbiguousResponseException(string reason, Exception? inner = null) : Exception(reason, inner);

/// <summary>
/// Thin, typed wrapper over the CodesWholesale v2 REST API. Unlike Bamboo/Visoria/GlobeTopper (one fixed
/// host), CodesWholesale has two genuinely different hosts (Sandbox/Production, see
/// <see cref="ResolveBaseUrl"/>) chosen per-<c>Supplier</c>-row, so every method takes the full
/// <see cref="SupplierProviderContext"/> rather than bare credentials. OAuth2 client-credentials access
/// tokens are cached per (supplier, environment, credential value) for <c>expires_in</c> minus a safety
/// margin — mirrors <c>EnebaHttpClient</c>'s identical cache-and-retry-once-on-401 pattern exactly, for
/// the same safety reason: a 401 means the request was rejected at the auth layer before any business
/// logic ran, so retrying with a fresh token can never double-execute a purchase.
/// </summary>
internal sealed class CodesWholesaleHttpClient(HttpClient httpClient, IOptions<CodesWholesaleProviderOptions> options, IMemoryCache cache, ILogger<CodesWholesaleHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ─── Environment / base URL ──────────────────────────────────────────

    internal static string ResolveBaseUrl(SupplierProviderContext context)
    {
        var settings = ParseSupplierSettings(context.SettingsJson);
        return string.Equals(settings?.Environment, "Production", StringComparison.OrdinalIgnoreCase)
            ? CodesWholesaleProviderConstants.ProductionBaseUrl
            : CodesWholesaleProviderConstants.SandboxBaseUrl;
    }

    private static CodesWholesaleSupplierSettings? ParseSupplierSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CodesWholesaleSupplierSettings>(settingsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static bool ResolveAllowPreOrder(SupplierProviderContext context) =>
        ParseSupplierSettings(context.SettingsJson)?.AllowPreOrder ?? false;

    // ─── OAuth ────────────────────────────────────────────────────────────

    private async Task<string> AcquireFreshAccessTokenAsync(string baseUrl, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + CodesWholesaleProviderConstants.OAuthTokenPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = CodesWholesaleProviderConstants.OAuthScope,
            }),
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CodesWholesaleAmbiguousResponseException("CodesWholesale token request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CodesWholesaleAmbiguousResponseException("CodesWholesale token request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            // Never log the raw body — it contains the access token itself.
            logger.LogDebug("CodesWholesale OAuth token request responded {StatusCode}.", (int)response.StatusCode);

            string raw;
            try
            {
                raw = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException)
            {
                throw new CodesWholesaleAmbiguousResponseException("CodesWholesale token response could not be read.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 400 or 401 or 403)
                {
                    string? description = null;
                    try
                    {
                        description = JsonSerializer.Deserialize<CodesWholesaleOAuthErrorResponse>(raw, JsonOptions)?.ErrorDescription;
                    }
                    catch (JsonException)
                    {
                        // Undocumented body shape on a token error — fall through with no description.
                    }

                    throw new CodesWholesaleApiException((int)response.StatusCode, null, description ?? "CodesWholesale rejected the configured Client ID/Client Secret.");
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new CodesWholesaleApiException(429, null, "CodesWholesale rate-limited the token request.");
                }

                throw new CodesWholesaleAmbiguousResponseException($"CodesWholesale token endpoint returned HTTP {(int)response.StatusCode}.");
            }

            CodesWholesaleTokenResponse? token;
            try
            {
                token = JsonSerializer.Deserialize<CodesWholesaleTokenResponse>(raw, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new CodesWholesaleAmbiguousResponseException("CodesWholesale token response was not valid JSON.", ex);
            }

            if (string.IsNullOrWhiteSpace(token?.AccessToken) || token.ExpiresIn is not > 0)
            {
                throw new CodesWholesaleAmbiguousResponseException("CodesWholesale token response was missing access_token/expires_in.");
            }

            var ttl = TimeSpan.FromSeconds(Math.Max(30, token.ExpiresIn.Value - 60));
            cache.Set(TokenCacheKey(baseUrl, clientId, clientSecret), token.AccessToken, ttl);
            return token.AccessToken;
        }
    }

    private async Task<string> GetAccessTokenAsync(string baseUrl, string clientId, string clientSecret, bool forceRefresh, CancellationToken cancellationToken)
    {
        var key = TokenCacheKey(baseUrl, clientId, clientSecret);
        if (!forceRefresh && cache.TryGetValue(key, out string? cached) && cached is not null)
        {
            return cached;
        }

        return await AcquireFreshAccessTokenAsync(baseUrl, clientId, clientSecret, cancellationToken);
    }

    /// <summary>Keyed by environment + the credential values themselves (not just SupplierId) so a credential rotation or a Sandbox→Production switch on the same Supplier row never serves a stale cached token for even one call.</summary>
    private static string TokenCacheKey(string baseUrl, string clientId, string clientSecret) =>
        $"codeswholesale:token:{baseUrl}:{clientId}:{clientSecret.GetHashCode()}";

    // ─── REST calls ───────────────────────────────────────────────────────

    public Task<CodesWholesaleAccount> GetAccountAsync(SupplierProviderContext context, CancellationToken cancellationToken) =>
        SendAsync<CodesWholesaleAccount>(context, HttpMethod.Get, CodesWholesaleProviderConstants.AccountPath, body: null, cancellationToken);

    /// <summary>
    /// <paramref name="productIds"/> batches every requested id into as few calls as the query string
    /// practically allows (see <c>CodesWholesaleSupplierProvider.GetAvailabilityAsync</c>'s chunking) —
    /// confirmed real filter (PHP SDK's <c>getProducts(["productIds" => [...]])</c> docblock). Passing
    /// no filters at all pulls the full price list in one call, matching <c>SearchCatalogAsync</c>'s use.
    /// </summary>
    public Task<CodesWholesaleProductListResponse> GetProductsAsync(
        SupplierProviderContext context, IReadOnlyList<string>? productIds, CancellationToken cancellationToken)
    {
        var path = CodesWholesaleProviderConstants.ProductsPath;
        if (productIds is { Count: > 0 })
        {
            path += "?productIds=" + Uri.EscapeDataString(string.Join(',', productIds));
        }

        return SendAsync<CodesWholesaleProductListResponse>(context, HttpMethod.Get, path, body: null, cancellationToken);
    }

    public Task<CodesWholesaleOrder> CreateOrderAsync(SupplierProviderContext context, CodesWholesaleOrderRequest order, CancellationToken cancellationToken) =>
        SendAsync<CodesWholesaleOrder>(context, HttpMethod.Post, CodesWholesaleProviderConstants.OrdersPath, order, cancellationToken);

    /// <summary><see langword="null"/> on a 404 — see <see cref="CodesWholesaleAmbiguousResponseException"/>'s remarks on why that's treated as ambiguous, not a definite "no such order", by the caller.</summary>
    public Task<CodesWholesaleOrder> GetOrderAsync(SupplierProviderContext context, string orderId, CancellationToken cancellationToken) =>
        SendAsync<CodesWholesaleOrder>(context, HttpMethod.Get,
            string.Format(CodesWholesaleProviderConstants.OrderByIdPathFormat, Uri.EscapeDataString(orderId)), body: null, cancellationToken);

    /// <summary>Reconciliation-only fallback for a purchase whose <c>orderId</c> was never captured — see <c>CodesWholesaleSupplierProvider.GetOrderStatusAsync</c>'s remarks.</summary>
    public Task<CodesWholesaleOrderListResponse> GetOrderHistoryAsync(
        SupplierProviderContext context, DateOnly startFrom, DateOnly endOn, CancellationToken cancellationToken) =>
        SendAsync<CodesWholesaleOrderListResponse>(context, HttpMethod.Get,
            string.Format(CodesWholesaleProviderConstants.OrderHistoryPathFormat, startFrom.ToString("yyyy-MM-dd"), endOn.ToString("yyyy-MM-dd")),
            body: null, cancellationToken);

    public Task<CodesWholesaleCode> GetCodeAsync(SupplierProviderContext context, string codeId, CancellationToken cancellationToken) =>
        SendAsync<CodesWholesaleCode>(context, HttpMethod.Get,
            string.Format(CodesWholesaleProviderConstants.CodeByIdPathFormat, Uri.EscapeDataString(codeId)), body: null, cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(
        SupplierProviderContext context, HttpMethod method, string path, object? body, CancellationToken cancellationToken, bool isRetry = false)
    {
        if (string.IsNullOrEmpty(context.Credentials.ApiKey) || string.IsNullOrEmpty(context.Credentials.ApiSecret))
        {
            throw new CodesWholesaleApiException(0, null, "CodesWholesale Client ID/Client Secret are not configured for this supplier.");
        }

        var baseUrl = ResolveBaseUrl(context);
        var token = await GetAccessTokenAsync(baseUrl, context.Credentials.ApiKey, context.Credentials.ApiSecret, forceRefresh: isRetry, cancellationToken);

        using var request = new HttpRequestMessage(method, baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CodesWholesaleAmbiguousResponseException("CodesWholesale request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CodesWholesaleAmbiguousResponseException("CodesWholesale request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            // A real 401 on a business call after a cached token was used means the token expired/was
            // revoked server-side before our cached TTL expected it to — safe to refresh and retry
            // exactly once, for the same reason EnebaHttpClient's identical retry is safe: a 401 is
            // rejected at the auth layer before any resolver/mutation runs.
            if ((int)response.StatusCode == 401 && !isRetry)
            {
                logger.LogInformation("CodesWholesale call returned 401 for supplier {SupplierId} — refreshing token and retrying once.", context.SupplierId);
                return await SendAsync<TResponse>(context, method, path, body, cancellationToken, isRetry: true);
            }

            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > options.Value.MaxResponseBytes)
            {
                throw new CodesWholesaleAmbiguousResponseException(
                    $"CodesWholesale response declared {declaredLength} bytes, exceeding the configured {options.Value.MaxResponseBytes}-byte limit — refusing to read it.");
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
                throw new CodesWholesaleAmbiguousResponseException("CodesWholesale response exceeded the configured maximum size — refusing to read it.");
            }

            // Never log the raw body — it may contain delivered redemption codes (order/code responses).
            logger.LogDebug("CodesWholesale {Method} {Path} responded {StatusCode}.", method, path, (int)response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TResponse>(raw, JsonOptions);
                    return parsed ?? throw new CodesWholesaleAmbiguousResponseException("CodesWholesale returned an empty/null success body.");
                }
                catch (JsonException ex)
                {
                    throw new CodesWholesaleAmbiguousResponseException("CodesWholesale returned a success status with an unparsable body.", ex);
                }
            }

            HandleErrorResponse(response.StatusCode, raw);
            throw new InvalidOperationException("Unreachable — HandleErrorResponse always throws.");
        }
    }

    private static void HandleErrorResponse(HttpStatusCode statusCode, string raw)
    {
        CodesWholesaleErrorResponse? error = null;
        try
        {
            error = JsonSerializer.Deserialize<CodesWholesaleErrorResponse>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            // Undocumented/unparsable error body — fall through to status-code-only handling.
        }

        switch ((int)statusCode)
        {
            case 401:
            case 403:
                throw new CodesWholesaleApiException((int)statusCode, error?.Code, error?.Message ?? error?.DeveloperMessage);

            case 429:
                throw new CodesWholesaleApiException(429, error?.Code, error?.Message ?? "CodesWholesale rate-limited this request.");

            // A 404 on an order lookup is deliberately NOT a definite failure here — the documentation
            // never states whether it means "this order was never created" or "not yet visible" for a
            // just-submitted order. Treated as ambiguous; see CodesWholesaleAmbiguousResponseException's remarks.
            case 404 when raw.Length == 0 || error is null:
                throw new CodesWholesaleAmbiguousResponseException("CodesWholesale returned HTTP 404 with no parsable error body — outcome unknown.");

            case >= 500:
                throw new CodesWholesaleAmbiguousResponseException($"CodesWholesale returned HTTP {(int)statusCode} — outcome unknown.");

            default:
                // 400/404 with a parsed business error body (e.g. 10002 insufficient balance, 20001
                // product not found) are definite negative answers — never guessed beyond the raw facts.
                throw new CodesWholesaleApiException((int)statusCode, error?.Code, error?.Message ?? error?.DeveloperMessage);
        }
    }
}

file sealed class BoundedStreamLimitExceededException : Exception;

/// <summary>Wraps a stream and throws rather than silently truncating once <paramref name="maxBytes"/> is exceeded — duplicated from Bamboo/Visoria/GlobeTopper/Eneba's identical file-scoped helper deliberately; no shared base was introduced for one more provider.</summary>
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
