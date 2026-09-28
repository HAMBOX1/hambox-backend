using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Application.RateLimiting;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Cryptomus (crypto/USDT) checkout endpoints: initiation and status-poll are authenticated like
/// the rest of checkout, but the webhook is necessarily anonymous — Cryptomus's server calling in
/// carries no HAMBOX session. See <c>HandleCryptomusWebhookCommand</c> for why its body is still
/// never trusted to determine payment success by itself, signature or not.
/// </summary>
internal static class CryptomusPaymentEndpoints
{
    public static void MapCryptomusPaymentEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Commerce")
            .HasApiVersion(1);

        group.MapPost("checkout/cryptomus", async Task<Results<Ok<CryptomusCheckoutInitiationDto>, BadRequest<ProblemDetails>>> (
            [FromBody] InitiateCryptomusCheckoutRequest request,
            HttpContext httpContext,
            ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() is { Length: > 0 } ua ? ua : "unknown";
            var language = httpContext.Request.Headers["Accept-Language"].ToString() is { Length: > 0 } acceptLanguage
                ? acceptLanguage.Split(',')[0].Split(';')[0].Trim()
                : "en";

            var result = await sender.Send(new InitiateCryptomusCheckoutCommand(
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
        .WithName("InitiateCryptomusCheckout")
        .RequireAuthorization()
        .RequireCustomerContext()
        .RequireRateLimiting(CommerceRateLimitPolicies.CheckoutInitiation);

        group.MapGet("payments/cryptomus/{paymentAttemptId:guid}/status", async Task<Results<Ok<CryptomusPaymentStatusDto>, BadRequest<ProblemDetails>>> (
            Guid paymentAttemptId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCryptomusPaymentStatusQuery(paymentAttemptId));

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
        .WithName("GetCryptomusPaymentStatus")
        .RequireAuthorization()
        .RequireCustomerContext();

        // Anonymous, server-to-server: Cryptomus calls this directly. The sign-header check is a
        // fast first-pass filter, not the sole trust mechanism — see HandleCryptomusWebhookCommand.
        group.MapPost("payments/cryptomus/webhook", async Task<IResult> (
            HttpContext httpContext,
            ISender sender,
            IOptions<CryptomusSettings> cryptomusOptions) =>
        {
            httpContext.Request.EnableBuffering();
            using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            httpContext.Request.Body.Position = 0;

            if (!CryptomusWebhookSignatureVerifier.IsValid(rawBody, cryptomusOptions.Value))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var payload = System.Text.Json.JsonSerializer.Deserialize<CryptomusWebhookPayload>(rawBody);
            if (payload is not null)
            {
                await sender.Send(new HandleCryptomusWebhookCommand(
                    payload.OrderId, payload.Uuid, payload.Status, payload.IsFinal));
            }

            return Results.Ok();
        })
        .WithName("CryptomusWebhook")
        .AllowAnonymous()
        .RequireRateLimiting(CommerceRateLimitPolicies.PaymentCallback);
    }
}

internal sealed record InitiateCryptomusCheckoutRequest(string Email, string Country);

internal sealed record CryptomusWebhookPayload(
    string? Uuid,
    [property: System.Text.Json.Serialization.JsonPropertyName("order_id")] string? OrderId,
    string? Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("is_final")] bool? IsFinal);
