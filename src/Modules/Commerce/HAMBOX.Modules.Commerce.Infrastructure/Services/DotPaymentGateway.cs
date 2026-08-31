using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Server-to-server HTTP client for DOT's Partners OTP Landing Page API (GET Access Token, Check
/// Transaction Status) per the vendor-provided PDFs. Never logs the Authorization header, the
/// encrypted token contents, or raw response bodies — only result codes and correlation
/// identifiers. A single attempt per call; the caller decides whether/when to retry (see
/// <c>DotPaymentVerificationService</c> — Check Transaction Status is safe to repeat, GET Access
/// Token is not retried automatically by this class).
/// </summary>
internal sealed class DotPaymentGateway(
    HttpClient httpClient,
    IOptions<DotSettings> optionsAccessor,
    ILogger<DotPaymentGateway> logger) : IDotPaymentGateway
{
    private const string AccessTokenPath = "/lb2/get-access-token";
    private const string OtpLandingPagePath = "/otp-lp";
    private const string StatusByPartnerTxIdPathTemplate = "/lb2/otplp-transaction-status/get-by-partnertxid/{0}/{1}/{2}";
    private const string StatusByDotTxIdPathTemplate = "/lb2/otplp-transaction-status/get-by-dottxid/{0}/{1}/{2}";

    public async Task<Result<DotAccessTokenResult>> GetAccessTokenAsync(
        DotAccessTokenRequest request, CancellationToken cancellationToken = default)
    {
        var settings = optionsAccessor.Value;
        var query = string.Join('&',
            $"partner_id={Uri.EscapeDataString(settings.PartnerId)}",
            $"service_id={Uri.EscapeDataString(settings.ServiceId)}",
            $"partner_txid={Uri.EscapeDataString(request.PartnerTxId)}",
            $"op_id={Uri.EscapeDataString(request.OperatorId)}",
            $"rurl={Uri.EscapeDataString(request.RedirectUrl)}",
            $"amount={request.Amount.ToString(CultureInfo.InvariantCulture)}",
            $"partner_tx_timestamp={request.PartnerTxTimestampUnix}");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{AccessTokenPath}?{query}");
        ApplyAuthHeaders(httpRequest, settings, includePartnerIdHeader: false);

        var result = await SendAsync<AccessTokenResponse>(httpRequest, "GetAccessToken", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<DotAccessTokenResult>(result.Error);
        }

        var body = result.Value;
        return Result.Success(new DotAccessTokenResult(body.ResultCode, body.ResultDesc ?? string.Empty, body.Token));
    }

    public string BuildOtpLandingPageUrl(string token, DotAccessTokenRequest originalRequest)
    {
        var settings = optionsAccessor.Value;
        var query = string.Join('&',
            $"token={Uri.EscapeDataString(token)}",
            $"rurl={Uri.EscapeDataString(originalRequest.RedirectUrl)}",
            $"partner_tx_timestamp={originalRequest.PartnerTxTimestampUnix}",
            $"amount={originalRequest.Amount.ToString(CultureInfo.InvariantCulture)}");

        return $"{settings.BaseUrl.TrimEnd('/')}{OtpLandingPagePath}?{query}";
    }

    public Task<Result<DotTransactionStatusResult>> CheckTransactionStatusByPartnerTxIdAsync(
        string partnerTxId, string operatorId, CancellationToken cancellationToken = default) =>
        CheckTransactionStatusAsync(StatusByPartnerTxIdPathTemplate, partnerTxId, operatorId, cancellationToken);

    public Task<Result<DotTransactionStatusResult>> CheckTransactionStatusByDotTxIdAsync(
        string dotTxId, string operatorId, CancellationToken cancellationToken = default) =>
        CheckTransactionStatusAsync(StatusByDotTxIdPathTemplate, dotTxId, operatorId, cancellationToken);

    private async Task<Result<DotTransactionStatusResult>> CheckTransactionStatusAsync(
        string pathTemplate, string transactionIdentifier, string operatorId, CancellationToken cancellationToken)
    {
        var settings = optionsAccessor.Value;
        var path = string.Format(
            CultureInfo.InvariantCulture,
            pathTemplate,
            Uri.EscapeDataString(operatorId),
            Uri.EscapeDataString(settings.ServiceId),
            Uri.EscapeDataString(transactionIdentifier));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuthHeaders(httpRequest, settings, includePartnerIdHeader: true);

        var result = await SendAsync<TransactionStatusResponse>(httpRequest, "CheckTransactionStatus", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<DotTransactionStatusResult>(result.Error);
        }

        var body = result.Value;
        DateTimeOffset? timestamp = null;
        // DOT's docs don't specify a timezone for this field — informational only (never used for
        // any payment-integrity decision, only display/audit), so a best-effort parse is acceptable.
        if (!string.IsNullOrWhiteSpace(body.TransactionTimeStamp)
            && DateTime.TryParseExact(
                body.TransactionTimeStamp, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed))
        {
            timestamp = parsed;
        }

        return Result.Success(new DotTransactionStatusResult(
            body.ResultCode, body.ResultDesc ?? string.Empty, timestamp, body.Amount, body.Currency));
    }

    private void ApplyAuthHeaders(HttpRequestMessage request, DotSettings settings, bool includePartnerIdHeader)
    {
        var basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        if (includePartnerIdHeader)
        {
            request.Headers.Add("PartnerId", settings.PartnerId);
        }
    }

    private async Task<Result<TBody>> SendAsync<TBody>(
        HttpRequestMessage request, string operationName, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed, not caller cancellation.
            logger.LogWarning("DOT {Operation} timed out.", operationName);
            return Result.Failure<TBody>(CommerceErrors.DotProviderUnavailable);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "DOT {Operation} request failed.", operationName);
            return Result.Failure<TBody>(CommerceErrors.DotProviderUnavailable);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Redirect
                || response.StatusCode == HttpStatusCode.MovedPermanently
                || response.StatusCode == HttpStatusCode.Found)
            {
                logger.LogWarning("DOT {Operation} returned an unexpected redirect.", operationName);
                return Result.Failure<TBody>(CommerceErrors.DotProviderUnavailable);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("DOT {Operation} returned HTTP {StatusCode}.", operationName, (int)response.StatusCode);
                return Result.Failure<TBody>(CommerceErrors.DotProviderUnavailable);
            }

            TBody? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<TBody>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "DOT {Operation} returned a malformed response body.", operationName);
                return Result.Failure<TBody>(CommerceErrors.DotProviderUnavailable);
            }

            if (body is null)
            {
                logger.LogWarning("DOT {Operation} returned an empty response body.", operationName);
                return Result.Failure<TBody>(CommerceErrors.DotProviderUnavailable);
            }

            return Result.Success(body);
        }
    }

    private sealed class AccessTokenResponse
    {
        [JsonPropertyName("resultCode")]
        public int ResultCode { get; init; }

        [JsonPropertyName("resultDesc")]
        public string? ResultDesc { get; init; }

        [JsonPropertyName("token")]
        public string? Token { get; init; }
    }

    private sealed class TransactionStatusResponse
    {
        [JsonPropertyName("resultCode")]
        public int ResultCode { get; init; }

        [JsonPropertyName("resultDesc")]
        public string? ResultDesc { get; init; }

        [JsonPropertyName("transactionTimeStamp")]
        public string? TransactionTimeStamp { get; init; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
    }
}
