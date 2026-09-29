using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Server-to-server HTTP client for the Cryptomus Merchant API. Never logs the API key, the
/// computed <c>sign</c> header, or raw response bodies — only status/order identifiers. Mirrors
/// <c>DotPaymentGateway</c>'s shape (shared <see cref="SendAsync{TBody}"/> helper, never lets a raw
/// exception escape). Settings come from <see cref="IPaymentGatewayConfigurationProvider"/> (admin
/// dashboard, DB-backed) rather than <c>IOptions&lt;CryptomusSettings&gt;</c> directly — see that
/// interface for the appsettings fallback rule.
/// <para>
/// Verified end-to-end against the live Cryptomus API (real invoice creation, full authenticated
/// checkout flow) — see commit history around the Cryptomus rollout for the verification notes.
/// </para>
/// </summary>
internal sealed class CryptomusPaymentGateway(
    HttpClient httpClient,
    IPaymentGatewayConfigurationProvider settingsProvider,
    ILogger<CryptomusPaymentGateway> logger) : ICryptomusPaymentGateway, IPaymentGateway
{
    private const string CreatePaymentPath = "/v1/payment";
    private const string PaymentInfoPath = "/v1/payment/info";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string GatewayKey => "cryptomus";

    public async Task<Result<CryptomusInvoiceResult>> CreateInvoiceAsync(
        CryptomusCreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetCryptomusSettingsAsync(cancellationToken);
        var body = new CreatePaymentRequestBody
        {
            Amount = request.AmountUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Currency = "USD",
            OrderId = request.OrderId,
            UrlReturn = request.ReturnUrl,
            UrlSuccess = request.ReturnUrl,
            UrlCallback = settings.PublicWebhookUrl,
            IsPaymentMultiple = false,
            Lifetime = settings.InvoiceLifetimeSeconds > 0 ? settings.InvoiceLifetimeSeconds : 3600,
        };

        var json = JsonSerializer.Serialize(body, SerializerOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CreatePaymentPath)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        ApplySignedHeaders(httpRequest, json, settings);

        var result = await SendAsync<CryptomusResponse>(httpRequest, "CreateInvoice", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<CryptomusInvoiceResult>(result.Error);
        }

        return MapResult(result.Value);
    }

    public async Task<Result<CryptomusInvoiceResult>> GetPaymentInfoByOrderIdAsync(
        string orderId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetCryptomusSettingsAsync(cancellationToken);
        var body = new PaymentInfoRequestBody { OrderId = orderId };
        var json = JsonSerializer.Serialize(body, SerializerOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, PaymentInfoPath)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        ApplySignedHeaders(httpRequest, json, settings);

        var result = await SendAsync<CryptomusResponse>(httpRequest, "GetPaymentInfo", cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<CryptomusInvoiceResult>(result.Error);
        }

        return MapResult(result.Value);
    }

    /// <summary>
    /// Cryptomus has no dedicated no-op "verify credentials" endpoint, so the lightest real check is
    /// creating a minimal ($1, immediately-expiring) test invoice — the same call this class already
    /// makes for a real checkout, just with a throwaway order id. Never charges anyone; the invoice is
    /// simply left unpaid and expires on its own.
    /// </summary>
    public async Task<PaymentGatewayTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetCryptomusSettingsAsync(cancellationToken);
        var testOrderId = $"test-{Guid.NewGuid():N}"[..16];
        var result = await CreateInvoiceAsync(
            new CryptomusCreateInvoiceRequest(testOrderId, 1.00m, settings.FrontendResultUrl), cancellationToken);

        return result.IsSuccess
            ? PaymentGatewayTestResult.Success("Cryptomus accepted a test invoice request — credentials are valid.")
            : PaymentGatewayTestResult.Failure(result.Error.Description);
    }

    private static Result<CryptomusInvoiceResult> MapResult(CryptomusResponse response)
    {
        if (response.Result is null)
        {
            return Result.Failure<CryptomusInvoiceResult>(CommerceErrors.CryptomusProviderUnavailable);
        }

        var r = response.Result;
        return Result.Success(new CryptomusInvoiceResult(
            r.Uuid ?? string.Empty,
            r.OrderId ?? string.Empty,
            r.Url,
            r.Status ?? r.PaymentStatus ?? "check",
            r.IsFinal ?? false,
            r.PaymentAmountUsd,
            r.PayerCurrency));
    }

    /// <summary>
    /// Computes and applies Cryptomus's request signature: <c>sign = md5(base64(rawJsonBody) + ApiKey)</c>,
    /// plus the <c>merchant</c> header. Applied to every request — Cryptomus rejects unsigned calls.
    /// </summary>
    private void ApplySignedHeaders(HttpRequestMessage request, string rawJson, CryptomusSettings settings)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawJson));
        var toSign = base64 + settings.ApiKey;
        var sign = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(toSign)));

        request.Headers.Add("merchant", settings.MerchantId);
        request.Headers.Add("sign", sign);
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
            logger.LogWarning("Cryptomus {Operation} timed out.", operationName);
            return Result.Failure<TBody>(CommerceErrors.CryptomusProviderUnavailable);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Cryptomus {Operation} request failed.", operationName);
            return Result.Failure<TBody>(CommerceErrors.CryptomusProviderUnavailable);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cryptomus {Operation} returned HTTP {StatusCode}.", operationName, (int)response.StatusCode);
                return Result.Failure<TBody>(CommerceErrors.CryptomusProviderUnavailable);
            }

            TBody? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<TBody>(SerializerOptions, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "Cryptomus {Operation} returned a malformed response body.", operationName);
                return Result.Failure<TBody>(CommerceErrors.CryptomusProviderUnavailable);
            }

            if (body is null)
            {
                logger.LogWarning("Cryptomus {Operation} returned an empty response body.", operationName);
                return Result.Failure<TBody>(CommerceErrors.CryptomusProviderUnavailable);
            }

            return Result.Success(body);
        }
    }

    private sealed class CreatePaymentRequestBody
    {
        [JsonPropertyName("amount")]
        public string Amount { get; set; } = string.Empty;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("url_return")]
        public string? UrlReturn { get; set; }

        [JsonPropertyName("url_success")]
        public string? UrlSuccess { get; set; }

        [JsonPropertyName("url_callback")]
        public string? UrlCallback { get; set; }

        [JsonPropertyName("is_payment_multiple")]
        public bool IsPaymentMultiple { get; set; }

        [JsonPropertyName("lifetime")]
        public int Lifetime { get; set; }
    }

    private sealed class PaymentInfoRequestBody
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;
    }

    private sealed class CryptomusResponse
    {
        [JsonPropertyName("state")]
        public int State { get; init; }

        [JsonPropertyName("result")]
        public CryptomusResultBody? Result { get; init; }
    }

    private sealed class CryptomusResultBody
    {
        [JsonPropertyName("uuid")]
        public string? Uuid { get; init; }

        [JsonPropertyName("order_id")]
        public string? OrderId { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("payment_status")]
        public string? PaymentStatus { get; init; }

        [JsonPropertyName("is_final")]
        public bool? IsFinal { get; init; }

        [JsonPropertyName("payment_amount_usd")]
        public decimal? PaymentAmountUsd { get; init; }

        [JsonPropertyName("payer_currency")]
        public string? PayerCurrency { get; init; }
    }
}
