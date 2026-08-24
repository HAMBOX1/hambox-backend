using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Server-to-server HTTP client for DOT's Fawry Direct Billing API (Direct Billing, Check
/// Transaction Status) per the vendor-provided "Direct Billing API Integration" PDF. A distinct DOT
/// product from the carrier-billing OTP flow (<c>DotPaymentGateway</c>) — different base path,
/// different auth headers (this API requires a <c>PartnerId</c> header on every call, including the
/// charge call, unlike the OTP product's GET Access Token), and a string-typed <c>resultCode</c>
/// rather than an integer. Never logs the Authorization header, MSISDN, or raw response bodies —
/// only result codes and correlation identifiers.
/// </summary>
internal sealed class DotFawryPaymentGateway(
    HttpClient httpClient,
    IOptions<DotFawrySettings> optionsAccessor,
    ILogger<DotFawryPaymentGateway> logger) : IDotFawryPaymentGateway
{
    public async Task<Result<DotFawryChargeResult>> ChargeAsync(
        DotFawryChargeRequest request, CancellationToken cancellationToken = default)
    {
        var settings = optionsAccessor.Value;
        var body = new ChargeRequestBody
        {
            PartnerTransId = request.PartnerTxId,
            OpId = ParseOperatorId(settings.OperatorId),
            Msisdn = request.Msisdn,
            Amount = request.Amount.ToString(CultureInfo.InvariantCulture),
            ServiceId = settings.ServiceId,
            ExtraField1 = request.CustomerEmail,
            ExtraField2 = request.CustomerName,
            ExtraField3 = System.Text.Json.JsonSerializer.Serialize(request.Items.Select(i => new ChargeItem
            {
                ItemId = i.ItemId,
                Description = i.Description,
                Price = i.Price,
                Quantity = i.Quantity,
            })),
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUrl(settings.BaseUrl, settings.DirectBillingPath))
        {
            Content = JsonContent.Create(body),
        };
        ApplyAuthHeaders(httpRequest, settings);

        var result = await SendAsync<ChargeResponseBody>(httpRequest, "DirectBilling", CommerceErrors.DotFawryProviderUnavailable, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<DotFawryChargeResult>(result.Error);
        }

        var response = result.Value;
        return Result.Success(new DotFawryChargeResult(
            response.ResultCode ?? string.Empty, response.ResultDesc ?? string.Empty, response.DotTransId, response.ExtraField1));
    }

    public Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusByPartnerTxIdAsync(
        string partnerTxId, CancellationToken cancellationToken = default) =>
        CheckTransactionStatusAsync("get-by-partnertransid", partnerTxId, cancellationToken);

    public Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusByDotTxIdAsync(
        string dotTxId, CancellationToken cancellationToken = default) =>
        CheckTransactionStatusAsync("get-by-dottransid", dotTxId, cancellationToken);

    private async Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusAsync(
        string lookupSegment, string transactionIdentifier, CancellationToken cancellationToken)
    {
        var settings = optionsAccessor.Value;
        var basePath = BuildUrl(settings.BaseUrl, settings.CheckTransactionStatusPath);
        var path = $"{basePath.TrimEnd('/')}/{lookupSegment}/" +
            $"{Uri.EscapeDataString(settings.OperatorId)}/{Uri.EscapeDataString(settings.ServiceId)}/{Uri.EscapeDataString(transactionIdentifier)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuthHeaders(httpRequest, settings);

        var result = await SendAsync<TransactionStatusResponseBody>(httpRequest, "CheckTransactionStatus", CommerceErrors.DotFawryProviderUnavailable, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<DotFawryTransactionStatusResult>(result.Error);
        }

        var body = result.Value;
        return Result.Success(new DotFawryTransactionStatusResult(
            body.ResultCode ?? string.Empty,
            body.ResultDesc ?? string.Empty,
            // DOT sends "" rather than omitting the field when it's not populated (e.g. ResultCode
            // != "0") — normalize to null here so every ?? fallback onto ResultCode/ResultDesc
            // downstream (DotFawryPaymentVerificationService) actually fires instead of silently
            // keeping an empty string.
            string.IsNullOrWhiteSpace(body.BillingTransactionResultCode) ? null : body.BillingTransactionResultCode,
            string.IsNullOrWhiteSpace(body.BillingTransactionResultDesc) ? null : body.BillingTransactionResultDesc,
            body.DotTransId));
    }

    private static string BuildUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path.Trim('/')}/";

    /// <summary>DOT's docs type <c>opId</c> as an Integer in the Direct Billing request body but as a path/string segment elsewhere — <see cref="DotFawrySettings.OperatorId"/> is stored as a string (matching every other configured id) and parsed here only where the wire format demands a number.</summary>
    private static int ParseOperatorId(string operatorId) =>
        int.TryParse(operatorId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private void ApplyAuthHeaders(HttpRequestMessage request, DotFawrySettings settings)
    {
        var basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("PartnerId", settings.PartnerId);
    }

    private async Task<Result<TBody>> SendAsync<TBody>(
        HttpRequestMessage request, string operationName, Error unavailableError, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("DOT Fawry {Operation} timed out.", operationName);
            return Result.Failure<TBody>(unavailableError);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "DOT Fawry {Operation} request failed.", operationName);
            return Result.Failure<TBody>(unavailableError);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently or HttpStatusCode.Found)
            {
                logger.LogWarning("DOT Fawry {Operation} returned an unexpected redirect.", operationName);
                return Result.Failure<TBody>(unavailableError);
            }

            // Per the Direct Billing spec, a "processing" result is still HTTP 200/201 — only
            // genuine transport/gateway failures (4xx/5xx) are treated as provider-unavailable here.
            if (!response.IsSuccessStatusCode && (int)response.StatusCode < 400)
            {
                logger.LogWarning("DOT Fawry {Operation} returned HTTP {StatusCode}.", operationName, (int)response.StatusCode);
                return Result.Failure<TBody>(unavailableError);
            }

            TBody? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<TBody>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "DOT Fawry {Operation} returned a malformed response body.", operationName);
                return Result.Failure<TBody>(unavailableError);
            }

            if (body is null)
            {
                logger.LogWarning("DOT Fawry {Operation} returned an empty response body.", operationName);
                return Result.Failure<TBody>(unavailableError);
            }

            return Result.Success(body);
        }
    }

    private sealed class ChargeRequestBody
    {
        [JsonPropertyName("partnerTransId")]
        public string PartnerTransId { get; init; } = string.Empty;

        [JsonPropertyName("opId")]
        public int OpId { get; init; }

        [JsonPropertyName("msisdn")]
        public string Msisdn { get; init; } = string.Empty;

        [JsonPropertyName("amount")]
        public string Amount { get; init; } = string.Empty;

        [JsonPropertyName("serviceId")]
        public string ServiceId { get; init; } = string.Empty;

        [JsonPropertyName("extraField1")]
        public string? ExtraField1 { get; init; }

        [JsonPropertyName("extraField2")]
        public string? ExtraField2 { get; init; }

        [JsonPropertyName("extraField3")]
        public string? ExtraField3 { get; init; }
    }

    private sealed class ChargeItem
    {
        [JsonPropertyName("itemId")]
        public string ItemId { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }
    }

    private sealed class ChargeResponseBody
    {
        [JsonPropertyName("resultCode")]
        public string? ResultCode { get; init; }

        [JsonPropertyName("resultDesc")]
        public string? ResultDesc { get; init; }

        [JsonPropertyName("dotTransId")]
        public string? DotTransId { get; init; }

        /// <summary>
        /// Not part of the generic Direct Billing response schema — confirmed by DOT (May
        /// Albalakha) as the Fawry reference number for this specific operator/service.
        /// </summary>
        [JsonPropertyName("extraField1")]
        public string? ExtraField1 { get; init; }
    }

    private sealed class TransactionStatusResponseBody
    {
        [JsonPropertyName("resultCode")]
        public string? ResultCode { get; init; }

        [JsonPropertyName("resultDesc")]
        public string? ResultDesc { get; init; }

        [JsonPropertyName("billingTransactionResultCode")]
        public string? BillingTransactionResultCode { get; init; }

        [JsonPropertyName("billingTransactionResultDesc")]
        public string? BillingTransactionResultDesc { get; init; }

        [JsonPropertyName("dotTransId")]
        public string? DotTransId { get; init; }
    }
}
