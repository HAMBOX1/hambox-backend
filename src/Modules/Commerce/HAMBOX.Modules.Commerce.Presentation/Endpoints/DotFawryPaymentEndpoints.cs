using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// DOT Fawry (Direct Billing) checkout endpoints — a distinct DOT product from the carrier-billing
/// OTP flow (<c>DotPaymentEndpoints</c>). Initiation is authenticated like the rest of checkout.
/// Unlike the OTP product, there is no browser redirect callback: DOT/Fawry SMS the customer a
/// reference number directly, so the only anonymous endpoint here is the server-to-server
/// notification webhook. Both initiate and notify treat their inputs as untrusted; see
/// <c>InitiateDotFawryCheckoutCommand</c>/<c>HandleDotFawryNotificationCommand</c> for why.
/// </summary>
internal static class DotFawryPaymentEndpoints
{
    public static void MapDotFawryPaymentEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Commerce")
            .HasApiVersion(1);

        group.MapPost("checkout/dot-fawry", async Task<Results<Ok<DotFawryCheckoutInitiationDto>, BadRequest<ProblemDetails>>> (
            [FromBody] InitiateDotFawryCheckoutRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new InitiateDotFawryCheckoutCommand(
                request.Email, request.Country, request.PhoneNumber, request.CustomerName, request.Wallet));

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
        .WithName("InitiateDotFawryCheckout")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapGet("payments/dot-fawry/{paymentAttemptId:guid}/status", async Task<Results<Ok<DotFawryPaymentStatusDto>, BadRequest<ProblemDetails>>> (
            Guid paymentAttemptId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetDotFawryPaymentStatusQuery(paymentAttemptId));

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
        .WithName("GetDotFawryPaymentStatus")
        .RequireAuthorization()
        .RequireCustomerContext();

        // Anonymous, server-to-server: DOT calls this directly, not the customer's browser. The
        // documented API has no signature/shared secret — see HandleDotFawryNotificationCommand for
        // why the payload is still never trusted to determine payment success by itself. IP
        // allowlisting is applied here as defense-in-depth only, never as the sole check. This is
        // the URL to register with DOT as the Fawry Direct Billing notification endpoint.
        group.MapPost("payments/dot-fawry/notify", async Task<IResult> (
            [FromBody] DotFawryNotificationRequest request,
            HttpContext httpContext,
            ISender sender) =>
        {
            if (!DotNotificationIpAllowlist.IsAllowed(httpContext))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            await sender.Send(new HandleDotFawryNotificationCommand(
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
        .WithName("DotFawryNotification")
        .AllowAnonymous();
    }
}

/// <param name="Wallet">One of "Fawry", "OrangeCash", "VodafoneCash" (a <c>DotFawryWalletOperator</c> member name) — defaults to "Fawry" for any caller that predates the Egyptian mobile wallet extension.</param>
internal sealed record InitiateDotFawryCheckoutRequest(
    string Email, string Country, string PhoneNumber, string? CustomerName, string Wallet = "Fawry");

internal sealed record DotFawryNotificationRequest(
    string? DotTransId,
    string? PartnerTransId,
    int? OpId,
    string? Msisdn,
    string? Amount,
    string? ServiceId,
    string? ResultCode,
    string? ResultDesc);
