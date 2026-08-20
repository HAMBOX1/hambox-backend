using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.ClaimGuestAlertSubscriptions;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.CreateAlertSubscription;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.GetMyAlertSubscriptions;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.RemoveAlertSubscription;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Registers customer "Notify Me" alert-subscription endpoints. Anonymous create/delete reuse the
/// same <c>X-Guest-Cart-Id</c> header the cart already uses for guest identity — the guest session
/// concept is not duplicated, only its header name is reused for a second purpose.
/// </summary>
internal static class CustomerAlertEndpoints
{
    private const string GuestSessionHeader = "X-Guest-Cart-Id";

    public static void MapCustomerAlertEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/customer-alerts")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("CustomerAlerts")
            .HasApiVersion(1);

        group.MapPost("", async (
            [FromBody] CreateAlertSubscriptionRequest request,
            [FromHeader(Name = GuestSessionHeader)] string? guestSessionId,
            ISender sender) =>
        {
            var result = await sender.Send(new CreateAlertSubscriptionCommand(request.VariantId, request.AlertType, guestSessionId));
            return ToResult(result);
        })
        .WithName("CreateAlertSubscription")
        .AllowAnonymous();

        group.MapGet("", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMyAlertSubscriptionsQuery());
            return ToResult(result);
        })
        .WithName("GetMyAlertSubscriptions")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapDelete("{id:guid}", async (
            Guid id,
            [FromHeader(Name = GuestSessionHeader)] string? guestSessionId,
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveAlertSubscriptionCommand(id, guestSessionId));
            return ToResult(result);
        })
        .WithName("RemoveAlertSubscription")
        .AllowAnonymous();

        group.MapPost("claim", async (
            [FromHeader(Name = GuestSessionHeader)] string? guestSessionId,
            ISender sender) =>
        {
            var result = await sender.Send(new ClaimGuestAlertSubscriptionsCommand(guestSessionId));
            return ToResult(result);
        })
        .WithName("ClaimGuestAlertSubscriptions")
        .RequireAuthorization()
        .RequireCustomerContext();
    }

    private static IResult ToResult(Result result) =>
        result.IsSuccess
            ? Results.Ok()
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });

    private static IResult ToResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
}

internal sealed record CreateAlertSubscriptionRequest(Guid VariantId, CustomerAlertType AlertType);
