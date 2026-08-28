using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;

/// <summary>
/// A response Eneba itself gave a definite (non-ambiguous) negative answer to — a real 401 (after the
/// one-time token-refresh retry already failed, see <see cref="EnebaHttpClient.ExecuteGraphQlAsync{TData}"/>),
/// 403, 429, or a parsed GraphQL <c>errors</c> array with no usable captured id. Never thrown for a
/// captured <c>orderId</c>/<c>actionId</c> — see that method's remarks.
/// </summary>
internal sealed class EnebaApiException(int httpStatusCode, string? rawMessage)
    : Exception(rawMessage ?? $"Eneba API returned HTTP {httpStatusCode}.")
{
    public int HttpStatusCode { get; } = httpStatusCode;
}

/// <summary>
/// The outcome could not be determined with confidence — timeout, connection failure, a real 5xx, or an
/// unparsable/unexpected response shape. Callers must never treat this as failure — only
/// <see cref="EnebaSupplierProvider.GetOrderStatusAsync"/> can resolve it, and — a genuine, documented
/// limitation of this API (no idempotency key, no lookup-by-client-reference anywhere) — it can only do
/// that when an <c>orderId</c> was already captured; see that method's remarks.
/// </summary>
internal sealed class EnebaAmbiguousResponseException(string reason, Exception? inner = null) : Exception(reason, inner);

/// <summary>
/// Thin, typed wrapper over the Eneba GraphQL API this integration uses. Unlike Bamboo/Visoria/
/// GlobeTopper (static Basic/Bearer/API-key auth, no state), Eneba needs a short-lived OAuth2 access
/// token acquired via client-credentials — <see cref="GetAccessTokenAsync"/> caches it per supplier
/// (keyed by <c>SupplierId</c>, never across suppliers) for <c>expires_in</c> minus a safety margin, and
/// <see cref="ExecuteGraphQlAsync{TData}"/> transparently re-acquires and retries exactly once on a real
/// 401 (safe: a 401 means the request was rejected at the auth layer before any resolver ran, so retrying
/// with a fresh token can never double-execute a mutation — see that method's remarks). Credentials are
/// still never cached on the shared <see cref="HttpClient"/>'s default headers — the bearer header is
/// built fresh per call from the token this returns.
/// </summary>
internal sealed class EnebaHttpClient(HttpClient httpClient, IOptions<EnebaProviderOptions> options, IMemoryCache cache, ILogger<EnebaHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ─── OAuth ────────────────────────────────────────────────────────────

    /// <summary>Internal (not private) so <see cref="EnebaSupplierProvider"/> can also read <see cref="EnebaOAuthSettings.AccountEmail"/> for key-export archive decryption — the one field in this blob that isn't an API credential.</summary>
    internal static EnebaOAuthSettings ParseSettings(SupplierProviderCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.OAuthSettingsJson))
        {
            throw new EnebaApiException(0, "Eneba Auth ID/Auth Secret are not configured for this supplier.");
        }

        EnebaOAuthSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<EnebaOAuthSettings>(credentials.OAuthSettingsJson, JsonOptions);
        }
        catch (JsonException)
        {
            throw new EnebaApiException(0, "Eneba OAuth settings are not valid JSON — expected {\"authId\":\"...\",\"authSecret\":\"...\",\"accountEmail\":\"...\"}.");
        }

        if (string.IsNullOrWhiteSpace(settings?.AuthId) || string.IsNullOrWhiteSpace(settings.AuthSecret))
        {
            throw new EnebaApiException(0, "Eneba Auth ID/Auth Secret are not configured for this supplier.");
        }

        return settings;
    }

    /// <summary>Never logged, never returned outside this class — see the type's own remarks and <see cref="ExecuteGraphQlAsync{TData}"/>'s Authorization-header construction.</summary>
    private async Task<string> AcquireFreshAccessTokenAsync(EnebaOAuthSettings settings, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EnebaProviderConstants.OAuthTokenUrl);
        var form = new Dictionary<string, string>
        {
            ["client_id"] = EnebaProviderConstants.OAuthClientId,
            ["grant_type"] = EnebaProviderConstants.OAuthGrantType,
            ["id"] = settings.AuthId!,
            ["secret"] = settings.AuthSecret!,
        };
        request.Content = new FormUrlEncodedContent(form);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EnebaAmbiguousResponseException("Eneba token request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EnebaAmbiguousResponseException("Eneba token request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            // Never log the raw body — even though a token response contains no license/redemption data,
            // it does contain the access token itself.
            logger.LogDebug("Eneba OAuth token request responded {StatusCode}.", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 401 or 403)
                {
                    throw new EnebaApiException((int)response.StatusCode, "Eneba rejected the configured Auth ID/Auth Secret.");
                }

                if ((int)response.StatusCode is 429)
                {
                    throw new EnebaApiException(429, "Eneba rate-limited the token request.");
                }

                if ((int)response.StatusCode >= 500)
                {
                    throw new EnebaAmbiguousResponseException($"Eneba token endpoint returned HTTP {(int)response.StatusCode}.");
                }

                throw new EnebaApiException((int)response.StatusCode, null);
            }

            string raw;
            try
            {
                raw = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException)
            {
                throw new EnebaAmbiguousResponseException("Eneba token response could not be read.", ex);
            }

            EnebaOAuthTokenResponse? token;
            try
            {
                token = JsonSerializer.Deserialize<EnebaOAuthTokenResponse>(raw, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new EnebaAmbiguousResponseException("Eneba token response was not valid JSON.", ex);
            }

            if (string.IsNullOrWhiteSpace(token?.AccessToken) || token.ExpiresIn is not > 0)
            {
                throw new EnebaAmbiguousResponseException("Eneba token response was missing access_token/expires_in.");
            }

            var ttl = TimeSpan.FromSeconds(Math.Max(30, token.ExpiresIn.Value - 60));
            cache.Set(TokenCacheKey(settings), token.AccessToken, ttl);
            return token.AccessToken;
        }
    }

    private async Task<string> GetAccessTokenAsync(EnebaOAuthSettings settings, bool forceRefresh, CancellationToken cancellationToken)
    {
        var key = TokenCacheKey(settings);
        if (!forceRefresh && cache.TryGetValue(key, out string? cached) && cached is not null)
        {
            return cached;
        }

        return await AcquireFreshAccessTokenAsync(settings, cancellationToken);
    }

    /// <summary>Keyed by the credential values themselves (not just SupplierId) so a credential rotation on the same Supplier row never serves a stale cached token for even one call.</summary>
    private static string TokenCacheKey(EnebaOAuthSettings settings) =>
        $"eneba:token:{settings.AuthId}:{settings.AuthSecret?.GetHashCode()}";

    // ─── GraphQL ──────────────────────────────────────────────────────────

    public Task<EnebaGraphQlResponse<EnebaWholesaleAuctionsData>> SearchWholesaleAuctionsAsync(
        Guid supplierId, SupplierProviderCredentials credentials, string? search, IReadOnlyList<string>? productIds, int first, string? after, CancellationToken cancellationToken) =>
        ExecuteGraphQlAsync<EnebaWholesaleAuctionsData>(
            supplierId, credentials, EnebaContracts.WholesaleAuctionsQuery,
            new { search, productIds, first, after }, cancellationToken);

    public Task<EnebaGraphQlResponse<EnebaPurchaseWholesaleAuctionsData>> PurchaseWholesaleAuctionsAsync(
        Guid supplierId, SupplierProviderCredentials credentials, string auctionId, int quantity, CancellationToken cancellationToken) =>
        ExecuteGraphQlAsync<EnebaPurchaseWholesaleAuctionsData>(
            supplierId, credentials, EnebaContracts.PurchaseWholesaleAuctionsMutation,
            new { input = new EnebaPurchaseInput([new EnebaPurchaseItem(auctionId, quantity)]) }, cancellationToken);

    public Task<EnebaGraphQlResponse<EnebaActionData>> GetActionAsync(
        Guid supplierId, SupplierProviderCredentials credentials, string actionId, CancellationToken cancellationToken) =>
        ExecuteGraphQlAsync<EnebaActionData>(supplierId, credentials, EnebaContracts.ActionQuery, new { actionId }, cancellationToken);

    public Task<EnebaGraphQlResponse<EnebaOrdersData>> GetOrdersAsync(
        Guid supplierId, SupplierProviderCredentials credentials, IReadOnlyList<string> orderIds, CancellationToken cancellationToken) =>
        ExecuteGraphQlAsync<EnebaOrdersData>(supplierId, credentials, EnebaContracts.OrdersQuery, new { orderIds }, cancellationToken);

    public Task<EnebaGraphQlResponse<EnebaExportOrderKeysData>> ExportOrderKeysAsync(
        Guid supplierId, SupplierProviderCredentials credentials, string entryToken, CancellationToken cancellationToken) =>
        ExecuteGraphQlAsync<EnebaExportOrderKeysData>(
            supplierId, credentials, EnebaContracts.ExportOrderKeysMutation, new { input = new EnebaExportOrderKeysInput(entryToken) }, cancellationToken);

    public Task<EnebaGraphQlResponse<EnebaOrderExportData>> GetOrderExportAsync(
        Guid supplierId, SupplierProviderCredentials credentials, string entryToken, CancellationToken cancellationToken) =>
        ExecuteGraphQlAsync<EnebaOrderExportData>(supplierId, credentials, EnebaContracts.OrderExportQuery, new { entryToken }, cancellationToken);

    /// <summary>
    /// Posts one GraphQL operation and deserializes its <c>data</c> field. On a real HTTP 401, re-acquires
    /// a fresh token and retries exactly once — safe for every operation this client sends, including the
    /// purchase mutation, because a 401 is Eneba's documented "not authenticated" response: it means the
    /// request was rejected before any resolver (and therefore before any mutation side effect) ran. This
    /// is categorically different from a timeout/connection failure/5xx, where whether the server-side
    /// resolver ran is genuinely unknown — those still throw <see cref="EnebaAmbiguousResponseException"/>
    /// and are never retried here.
    /// </summary>
    private async Task<EnebaGraphQlResponse<TData>> ExecuteGraphQlAsync<TData>(
        Guid supplierId, SupplierProviderCredentials credentials, string query, object? variables, CancellationToken cancellationToken, bool isRetry = false)
    {
        var settings = ParseSettings(credentials);
        var token = await GetAccessTokenAsync(settings, forceRefresh: isRetry, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, EnebaProviderConstants.GraphQlPath)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new EnebaGraphQlRequest(query, variables), JsonOptions), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EnebaAmbiguousResponseException("Eneba GraphQL request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EnebaAmbiguousResponseException("Eneba GraphQL request failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            if ((int)response.StatusCode == 401 && !isRetry)
            {
                logger.LogInformation("Eneba GraphQL call returned 401 for supplier {SupplierId} — refreshing token and retrying once.", supplierId);
                return await ExecuteGraphQlAsync<TData>(supplierId, credentials, query, variables, cancellationToken, isRetry: true);
            }

            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > options.Value.MaxResponseBytes)
            {
                throw new EnebaAmbiguousResponseException(
                    $"Eneba response declared {declaredLength} bytes, exceeding the configured {options.Value.MaxResponseBytes}-byte limit — refusing to read it.");
            }

            string raw;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var bounded = new EnebaBoundedReadStream(stream, options.Value.MaxResponseBytes);
                using var reader = new StreamReader(bounded, Encoding.UTF8);
                raw = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (EnebaBoundedStreamLimitExceededException)
            {
                throw new EnebaAmbiguousResponseException("Eneba response exceeded the configured maximum size — refusing to read it.");
            }

            // Never log the raw body — it may contain license/redemption data on the order-related queries.
            logger.LogDebug("Eneba GraphQL {Query} responded {StatusCode}.", GraphQlOperationName(query), (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                HandleHttpErrorResponse(response.StatusCode);
                throw new InvalidOperationException("Unreachable — HandleHttpErrorResponse always throws.");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<EnebaGraphQlResponse<TData>>(raw, JsonOptions);
                return parsed ?? throw new EnebaAmbiguousResponseException("Eneba returned an empty/null GraphQL response body.");
            }
            catch (JsonException ex)
            {
                throw new EnebaAmbiguousResponseException("Eneba returned a success status with an unparsable GraphQL body.", ex);
            }
        }
    }

    private static void HandleHttpErrorResponse(HttpStatusCode statusCode)
    {
        switch ((int)statusCode)
        {
            case 401:
            case 403:
                throw new EnebaApiException((int)statusCode, null);

            case 429:
                throw new EnebaApiException(429, null);

            case >= 500:
                throw new EnebaAmbiguousResponseException($"Eneba returned HTTP {(int)statusCode} — outcome unknown.");

            default:
                throw new EnebaApiException((int)statusCode, null);
        }
    }

    private static string GraphQlOperationName(string query)
    {
        var queryIndex = query.IndexOf("query ", StringComparison.Ordinal);
        var mutationIndex = query.IndexOf("mutation ", StringComparison.Ordinal);

        int afterKeyword;
        if (queryIndex >= 0)
        {
            afterKeyword = queryIndex + "query ".Length;
        }
        else if (mutationIndex >= 0)
        {
            afterKeyword = mutationIndex + "mutation ".Length;
        }
        else
        {
            return "unknown";
        }

        var end = query.IndexOfAny(['(', '{', ' ', '\n'], afterKeyword);
        return end < 0 ? query[afterKeyword..].Trim() : query[afterKeyword..end].Trim();
    }

    // ─── Key export archive download (not GraphQL — a plain presigned-URL GET) ─────────────────────────

    /// <summary>Downloads the encrypted key-export archive from the presigned <c>downloadUrl</c> — never routed through the GraphQL endpoint, and never authenticated with the Eneba bearer token (the URL is itself the credential, per how presigned URLs work).</summary>
    public async Task<byte[]> DownloadArchiveAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new EnebaAmbiguousResponseException("Eneba's export downloadUrl was missing or not a valid HTTPS URL.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EnebaAmbiguousResponseException("Eneba archive download timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EnebaAmbiguousResponseException("Eneba archive download failed at the network/TLS layer.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EnebaAmbiguousResponseException($"Eneba archive download returned HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > options.Value.MaxArchiveBytes)
            {
                throw new EnebaAmbiguousResponseException(
                    $"Eneba archive declared {declaredLength} bytes, exceeding the configured {options.Value.MaxArchiveBytes}-byte limit — refusing to download it.");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var bounded = new EnebaBoundedReadStream(stream, options.Value.MaxArchiveBytes);
                using var memory = new MemoryStream();
                await bounded.CopyToAsync(memory, cancellationToken);
                return memory.ToArray();
            }
            catch (EnebaBoundedStreamLimitExceededException)
            {
                throw new EnebaAmbiguousResponseException("Eneba archive exceeded the configured maximum size — refusing to read it.");
            }
        }
    }
}

file sealed class EnebaBoundedStreamLimitExceededException : Exception;

/// <summary>Wraps a stream and throws rather than silently truncating once <paramref name="maxBytes"/> is exceeded — duplicated from Bamboo/Visoria/GlobeTopper's identical file-scoped helper deliberately; no shared base was introduced for one more provider.</summary>
file sealed class EnebaBoundedReadStream(Stream inner, int maxBytes) : Stream
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
            throw new EnebaBoundedStreamLimitExceededException();
        }

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        _readSoFar += read;
        if (_readSoFar > maxBytes)
        {
            throw new EnebaBoundedStreamLimitExceededException();
        }

        return read;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
