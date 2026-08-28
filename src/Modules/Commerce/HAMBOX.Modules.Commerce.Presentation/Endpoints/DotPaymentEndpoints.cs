using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// DOT (direct-carrier-billing) checkout endpoints: initiation is authenticated like the rest of
/// checkout, but the callback and notification endpoints are necessarily anonymous — DOT's redirect
/// carries no HAMBOX session, and the notification webhook is a separate server calling in. Both
/// treat their inputs as untrusted; see <c>HandleDotRedirectCallbackCommand</c>/
/// <c>HandleDotNotificationCommand</c> for why.
/// </summary>
internal static class DotPaymentEndpoints
{
    public static void MapDotPaymentEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Commerce")
            .HasApiVersion(1);

        group.MapPost("checkout/dot", async Task<Results<Ok<DotCheckoutInitiationDto>, BadRequest<ProblemDetails>>> (
            [FromBody] InitiateDotCheckoutRequest request,
            HttpContext httpContext,
            ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() is { Length: > 0 } ua ? ua : "unknown";
            var language = httpContext.Request.Headers["Accept-Language"].ToString() is { Length: > 0 } acceptLanguage
                ? acceptLanguage.Split(',')[0].Split(';')[0].Trim()
                : "en";

            var result = await sender.Send(new InitiateDotCheckoutCommand(
                request.Email, request.Country, request.Wallet, ipAddress, userAgent, language));

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
        .WithName("InitiateDotCheckout")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapGet("payments/dot/{paymentAttemptId:guid}/status", async Task<Results<Ok<DotPaymentStatusDto>, BadRequest<ProblemDetails>>> (
            Guid paymentAttemptId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetDotPaymentStatusQuery(paymentAttemptId));

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
        .WithName("GetDotPaymentStatus")
        .RequireAuthorization()
        .RequireCustomerContext();

        // Anonymous: this is the customer's own browser landing back from DOT with no HAMBOX
        // session attached. Every query parameter here is treated as untrusted input — see
        // HandleDotRedirectCallbackCommand. Always redirects the browser onward; never returns JSON.
        group.MapGet("payments/dot/callback", async (
            [FromQuery(Name = "partner_txid")] string? partnerTxId,
            [FromQuery(Name = "dot_txid")] string? dotTxId,
            [FromQuery(Name = "op_id")] int? opId,
            [FromQuery(Name = "service_id")] string? serviceId,
            [FromQuery(Name = "msisdn")] string? msisdn,
            [FromQuery(Name = "reason_code")] string? reasonCode,
            [FromQuery(Name = "reason_desc")] string? reasonDesc,
            ISender sender,
            IOptions<DotSettings> dotOptions) =>
        {
            var result = await sender.Send(new HandleDotRedirectCallbackCommand(
                partnerTxId, dotTxId, opId, serviceId, msisdn, reasonCode, reasonDesc));

            var frontendBase = dotOptions.Value.FrontendResultUrl.TrimEnd('/');
            var target = result.IsSuccess
                ? $"{frontendBase}?paymentAttemptId={result.Value}"
                : $"{frontendBase}?error=invalid_callback";

            return Results.Redirect(target);
        })
        .WithName("DotRedirectCallback")
        .AllowAnonymous();

        // Anonymous, server-to-server: DOT calls this directly, not the customer's browser. The
        // documented API has no signature/shared secret — see HandleDotNotificationCommand for why
        // the payload is still never trusted to determine payment success by itself. IP allowlisting
        // is applied here as defense-in-depth only, never as the sole check.
        group.MapPost("payments/dot/notify", async Task<IResult> (
            [FromBody] DotNotificationRequest request,
            HttpContext httpContext,
            ISender sender) =>
        {
            if (!DotNotificationIpAllowlist.IsAllowed(httpContext))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            await sender.Send(new HandleDotNotificationCommand(
                request.DotTransId,
                request.PartnerTransId,
                request.OpId,
                request.Msisdn,
                request.Amount,
                request.ServiceId,
                request.ResultCode,
                request.ResultDesc));

            // Per the documented contract: any response other than exactly "1" is treated as
            // failure and DOT retries the same notification later in the day.
            return Results.Text("1", "text/plain");
        })
        .WithName("DotNotification")
        .AllowAnonymous();
    }
}

/// <param name="Wallet">One of "OrangeCash", "VodafoneCash" (a <c>DotWalletOperator</c> member name).</param>
internal sealed record InitiateDotCheckoutRequest(string Email, string Country, string Wallet);

internal sealed record DotNotificationRequest(
    string? DotTransId,
    string? PartnerTransId,
    int? OpId,
    string? Msisdn,
    string? Amount,
    string? ServiceId,
    string? ResultCode,
    string? ResultDesc);
