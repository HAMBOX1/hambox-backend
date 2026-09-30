using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;
using HAMBOX.Modules.Commerce.Application.RateLimiting;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// OxaPay (crypto) checkout endpoints: initiation and status-poll are authenticated like the rest of
/// checkout, but the webhook is necessarily anonymous — OxaPay's server calling in carries no HAMBOX
/// session. See <c>HandleOxaPayWebhookCommand</c> for why its body is still never trusted to
/// determine payment success by itself, signature or not. A second, independent crypto gateway
/// alongside Cryptomus — see <c>CryptomusPaymentEndpoints</c> for the identical shape.
/// </summary>
internal static class OxaPayPaymentEndpoints
{
    public static void MapOxaPayPaymentEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Commerce")
            .HasApiVersion(1);

        group.MapPost("checkout/oxapay", async Task<Results<Ok<OxaPayCheckoutInitiationDto>, BadRequest<ProblemDetails>>> (
            [FromBody] InitiateOxaPayCheckoutRequest request,
            HttpContext httpContext,
            ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() is { Length: > 0 } ua ? ua : "unknown";
            var language = httpContext.Request.Headers["Accept-Language"].ToString() is { Length: > 0 } acceptLanguage
                ? acceptLanguage.Split(',')[0].Split(';')[0].Trim()
                : "en";

            var result = await sender.Send(new InitiateOxaPayCheckoutCommand(
                request.Email, request.Country, ipAddress, userAgent, language));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });
        })
        .WithName("InitiateOxaPayCheckout")
        .RequireAuthorization()
        .RequireCustomerContext()
        .RequireRateLimiting(CommerceRateLimitPolicies.CheckoutInitiation);

        group.MapGet("payments/oxapay/{paymentAttemptId:guid}/status", async Task<Results<Ok<OxaPayPaymentStatusDto>, BadRequest<ProblemDetails>>> (
            Guid paymentAttemptId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetOxaPayPaymentStatusQuery(paymentAttemptId));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });
        })
        .WithName("GetOxaPayPaymentStatus")
        .RequireAuthorization()
        .RequireCustomerContext();

        // Anonymous, server-to-server: OxaPay calls this directly. The HMAC-header check is a fast
        // first-pass filter, not the sole trust mechanism — see HandleOxaPayWebhookCommand. The
        // signing key only ever lives in the admin-managed DB row (no appsettings fallback), so this
        // reads it via IPaymentGatewayConfigurationProvider rather than IOptions<OxaPaySettings>.
        group.MapPost("payments/oxapay/webhook", async Task<IResult> (
            HttpContext httpContext,
            ISender sender,
            IPaymentGatewayConfigurationProvider settingsProvider) =>
        {
            httpContext.Request.EnableBuffering();
            using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            httpContext.Request.Body.Position = 0;

            var settings = await settingsProvider.GetOxaPaySettingsAsync(httpContext.RequestAborted);
            var hmacHeader = httpContext.Request.Headers["HMAC"].ToString();

            if (!OxaPayWebhookSignatureVerifier.IsValid(rawBody, hmacHeader, settings.MerchantApiKey))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var payload = System.Text.Json.JsonSerializer.Deserialize<OxaPayWebhookPayload>(rawBody);
            if (payload is not null)
            {
                await sender.Send(new HandleOxaPayWebhookCommand(payload.OrderId, payload.TrackId, payload.Status));
            }

            // OxaPay expects a bare "ok" body, not a JSON envelope.
            return Results.Text("ok");
        })
        .WithName("OxaPayWebhook")
        .AllowAnonymous()
        .RequireRateLimiting(CommerceRateLimitPolicies.PaymentCallback);
    }
}

internal sealed record InitiateOxaPayCheckoutRequest(string Email, string Country);

internal sealed record OxaPayWebhookPayload(
    [property: System.Text.Json.Serialization.JsonPropertyName("track_id")] string? TrackId,
    [property: System.Text.Json.Serialization.JsonPropertyName("order_id")] string? OrderId,
    string? Status);
