using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Server-to-server HTTP client for the OxaPay Merchant API. Never logs the API key or raw response
/// bodies — only status/order identifiers. Mirrors <c>CryptomusPaymentGateway</c>'s shape (shared
/// <see cref="SendAsync{TBody}"/> helper, never lets a raw exception escape). Settings come from
/// <see cref="IPaymentGatewayConfigurationProvider"/> (admin dashboard, DB-backed) — unlike
/// Cryptomus, there is no appsettings fallback for the API key, since it was only ever handed to
/// HAMBOX to enter into the dashboard.
/// <para>
/// The exact shape of OxaPay's Payment Information response (whether fields sit at the top level or
/// nested under <c>data</c>, matching the documented Generate Invoice response) has not been
/// confirmed against a live call yet — <see cref="OxaPayResponseEnvelope"/> tolerates both by trying
/// <c>data</c> first and falling back to the envelope's own fields. Verify with a real Test
/// Connection / invoice once real credentials are entered.
/// </para>
/// </summary>
internal sealed class OxaPayPaymentGateway(
    HttpClient httpClient,
    IPaymentGatewayConfigurationProvider settingsProvider,
    ILogger<OxaPayPaymentGateway> logger) : IOxaPayPaymentGateway, IPaymentGateway
{
    private const string CreateInvoicePath = "/v1/payment/invoice";
    private const string PaymentInfoPathPrefix = "/v1/payment/";
    private const string PaymentHistoryPath = "/v1/payment";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string GatewayKey => "oxapay";

    public async Task<Result<OxaPayInvoiceResult>> CreateInvoiceAsync(
        OxaPayCreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetOxaPaySettingsAsync(cancellationToken);
        var body = new CreateInvoiceRequestBody
        {
            Amount = request.AmountUsd,
            OrderId = request.OrderId,
            ReturnUrl = request.ReturnUrl,
            CallbackUrl = settings.PublicWebhookUrl,
            Lifetime = settings.InvoiceLifetimeMinutes > 0 ? settings.InvoiceLifetimeMinutes : 60,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CreateInvoicePath)
        {
            Content = JsonContent.Create(body, options: SerializerOptions),
        };
        ApplyAuthHeader(httpRequest, settings.MerchantApiKey);

        var result = await SendAsync<OxaPayResponseEnvelope>(httpRequest, "CreateInvoice", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<OxaPayInvoiceResult>(result.Error);
        }

        return MapCreateInvoiceResult(result.Value, request.OrderId);
    }

    public async Task<Result<OxaPayInvoiceResult>> GetPaymentInfoByTrackIdAsync(
        string trackId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetOxaPaySettingsAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, PaymentInfoPathPrefix + Uri.EscapeDataString(trackId));
        ApplyAuthHeader(httpRequest, settings.MerchantApiKey);

        var result = await SendAsync<OxaPayResponseEnvelope>(httpRequest, "GetPaymentInfo", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<OxaPayInvoiceResult>(result.Error);
        }

        return MapStatusResult(result.Value);
    }

    /// <summary>
    /// OxaPay has no dedicated no-op "verify credentials" endpoint for a single track id, so the
    /// lightest real check is Payment History (<c>GET /v1/payment?size=1</c>) — it requires a valid
    /// <c>merchant_api_key</c> but never creates, charges, or exposes anyone else's data.
    /// </summary>
    public async Task<PaymentGatewayTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetOxaPaySettingsAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{PaymentHistoryPath}?size=1");
        ApplyAuthHeader(httpRequest, settings.MerchantApiKey);

        var result = await SendAsync<OxaPayResponseEnvelope>(httpRequest, "TestConnection", cancellationToken);
        return result.IsSuccess
            ? PaymentGatewayTestResult.Success("OxaPay accepted a payment-history request — credentials are valid.")
            : PaymentGatewayTestResult.Failure(result.Error.Description);
    }

    private static Result<OxaPayInvoiceResult> MapCreateInvoiceResult(OxaPayResponseEnvelope response, string orderId)
    {
        var data = response.Data;
        if (data is null || string.IsNullOrWhiteSpace(data.TrackId))
        {
            return Result.Failure<OxaPayInvoiceResult>(CommerceErrors.OxaPayProviderUnavailable);
        }

        return Result.Success(new OxaPayInvoiceResult(
            data.TrackId,
            orderId,
            data.PaymentUrl,
            "new",
            null,
            null));
    }

    private static Result<OxaPayInvoiceResult> MapStatusResult(OxaPayResponseEnvelope response)
    {
        var data = response.Data ?? response.AsFallbackData();
        if (data is null || string.IsNullOrWhiteSpace(data.TrackId))
        {
            return Result.Failure<OxaPayInvoiceResult>(CommerceErrors.OxaPayProviderUnavailable);
        }

        return Result.Success(new OxaPayInvoiceResult(
            data.TrackId,
            data.OrderId,
            data.PaymentUrl,
            data.Status ?? "waiting",
            data.Amount,
            data.Currency));
    }

    private static void ApplyAuthHeader(HttpRequestMessage request, string merchantApiKey)
    {
        request.Headers.Add("merchant_api_key", merchantApiKey);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
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
            logger.LogWarning("OxaPay {Operation} timed out.", operationName);
            return Result.Failure<TBody>(CommerceErrors.OxaPayProviderUnavailable);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "OxaPay {Operation} request failed.", operationName);
            return Result.Failure<TBody>(CommerceErrors.OxaPayProviderUnavailable);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OxaPay {Operation} returned HTTP {StatusCode}.", operationName, (int)response.StatusCode);
                return Result.Failure<TBody>(CommerceErrors.OxaPayProviderUnavailable);
            }

            TBody? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<TBody>(SerializerOptions, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "OxaPay {Operation} returned a malformed response body.", operationName);
                return Result.Failure<TBody>(CommerceErrors.OxaPayProviderUnavailable);
            }

            if (body is null)
            {
                logger.LogWarning("OxaPay {Operation} returned an empty response body.", operationName);
                return Result.Failure<TBody>(CommerceErrors.OxaPayProviderUnavailable);
            }

            return Result.Success(body);
        }
    }

    private sealed class CreateInvoiceRequestBody
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("return_url")]
        public string? ReturnUrl { get; set; }

        [JsonPropertyName("callback_url")]
        public string? CallbackUrl { get; set; }

        [JsonPropertyName("lifetime")]
        public int Lifetime { get; set; }
    }

    /// <summary>
    /// Tolerates both a <c>{ data: {...} }</c> envelope (documented for Generate Invoice) and a flat
    /// top-level response (how the Payment Information excerpt available at implementation time read)
    /// — see the class doc comment.
    /// </summary>
    private sealed class OxaPayResponseEnvelope
    {
        [JsonPropertyName("data")]
        public OxaPayResultBody? Data { get; init; }

        [JsonPropertyName("track_id")]
        public string? TrackId { get; init; }

        [JsonPropertyName("order_id")]
        public string? OrderId { get; init; }

        [JsonPropertyName("payment_url")]
        public string? PaymentUrl { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }

        public OxaPayResultBody? AsFallbackData() =>
            string.IsNullOrWhiteSpace(TrackId)
                ? null
                : new OxaPayResultBody
                {
                    TrackId = TrackId,
                    OrderId = OrderId,
                    PaymentUrl = PaymentUrl,
                    Status = Status,
                    Amount = Amount,
                    Currency = Currency,
                };
    }

    private sealed class OxaPayResultBody
    {
        [JsonPropertyName("track_id")]
        public string? TrackId { get; init; }

        [JsonPropertyName("order_id")]
        public string? OrderId { get; init; }

        [JsonPropertyName("payment_url")]
        public string? PaymentUrl { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
    }
}
