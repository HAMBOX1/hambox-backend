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
/// Every OxaPay v1 response shares one envelope — <c>{ data, message, error, status, version }</c>,
/// confirmed live against Payment History (<c>GET /v1/payment</c>) during rollout — with the
/// endpoint-specific payload always nested under <c>data</c>. The envelope's own <c>status</c> is a
/// numeric HTTP-status echo, not the invoice's string status, so it must not share a class with any
/// endpoint's payload fields (an earlier version of this file made exactly that mistake and threw a
/// deserialization exception on every call once real credentials were entered).
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

        var result = await SendAsync<OxaPayEnvelope<CreateInvoiceData>>(httpRequest, "CreateInvoice", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<OxaPayInvoiceResult>(result.Error);
        }

        return MapCreateInvoiceResult(result.Value.Data, request.OrderId);
    }

    public async Task<Result<OxaPayInvoiceResult>> GetPaymentInfoByTrackIdAsync(
        string trackId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetOxaPaySettingsAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, PaymentInfoPathPrefix + Uri.EscapeDataString(trackId));
        ApplyAuthHeader(httpRequest, settings.MerchantApiKey);

        var result = await SendAsync<OxaPayEnvelope<PaymentInfoData>>(httpRequest, "GetPaymentInfo", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<OxaPayInvoiceResult>(result.Error);
        }

        return MapStatusResult(result.Value.Data);
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

        // Payload shape doesn't matter here — only that the call authenticates and returns 2xx —
        // so the generic parameter is `object?`, which System.Text.Json always deserializes
        // successfully (as a JsonElement) regardless of what `data` actually contains.
        var result = await SendAsync<OxaPayEnvelope<object?>>(httpRequest, "TestConnection", cancellationToken);
        return result.IsSuccess
            ? PaymentGatewayTestResult.Success("OxaPay accepted a payment-history request — credentials are valid.")
            : PaymentGatewayTestResult.Failure(result.Error.Description);
    }

    private static Result<OxaPayInvoiceResult> MapCreateInvoiceResult(CreateInvoiceData? data, string orderId)
    {
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

    private static Result<OxaPayInvoiceResult> MapStatusResult(PaymentInfoData? data)
    {
        if (data is null || string.IsNullOrWhiteSpace(data.TrackId))
        {
            return Result.Failure<OxaPayInvoiceResult>(CommerceErrors.OxaPayProviderUnavailable);
        }

        return Result.Success(new OxaPayInvoiceResult(
            data.TrackId,
            data.OrderId,
            null,
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
    /// The envelope every OxaPay v1 response shares — confirmed live (see class doc comment).
    /// <typeparamref name="TData"/> is whatever sits under <c>data</c> for that specific endpoint;
    /// <see cref="Status"/> here is the wrapper's own numeric HTTP-status echo, deliberately kept
    /// separate from any endpoint payload's own (string) invoice status.
    /// </summary>
    private sealed class OxaPayEnvelope<TData>
    {
        [JsonPropertyName("data")]
        public TData? Data { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("status")]
        public int Status { get; init; }
    }

    /// <summary>Generate Invoice's <c>data</c> (<c>POST /v1/payment/invoice</c>).</summary>
    private sealed class CreateInvoiceData
    {
        [JsonPropertyName("track_id")]
        public string? TrackId { get; init; }

        [JsonPropertyName("payment_url")]
        public string? PaymentUrl { get; init; }
    }

    /// <summary>Payment Information's <c>data</c> (<c>GET /v1/payment/{track_id}</c>).</summary>
    private sealed class PaymentInfoData
    {
        [JsonPropertyName("track_id")]
        public string? TrackId { get; init; }

        [JsonPropertyName("order_id")]
        public string? OrderId { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
    }
}
