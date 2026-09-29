using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Features.PaymentGateways;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class PaymentGatewayAdminEndpoints
{
    public static void MapPaymentGatewayAdminEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/payment-gateways")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("PaymentGateways")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetPaymentGatewaysQuery())))
            .RequirePermission(PermissionConstants.PaymentGateways.View);

        group.MapGet("{gatewayKey}", async Task<IResult> (string gatewayKey, ISender sender) =>
            MapResult(await sender.Send(new GetPaymentGatewayByKeyQuery(gatewayKey))))
            .RequirePermission(PermissionConstants.PaymentGateways.View);

        group.MapPut("{gatewayKey}", async Task<IResult> (
                string gatewayKey, [FromBody] UpdatePaymentGatewayGeneralRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdatePaymentGatewayGeneralCommand(gatewayKey, request))))
            .RequirePermission(PermissionConstants.PaymentGateways.Edit);

        group.MapPut("{gatewayKey}/credentials", async Task<IResult> (
                string gatewayKey, [FromBody] UpdatePaymentGatewayCredentialsRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdatePaymentGatewayCredentialsCommand(gatewayKey, request))))
            .RequirePermission(PermissionConstants.PaymentGateways.Edit);

        group.MapPost("{gatewayKey}/enable", async Task<IResult> (string gatewayKey, ISender sender) =>
            MapResult(await sender.Send(new SetPaymentGatewayEnabledCommand(gatewayKey, true))))
            .RequirePermission(PermissionConstants.PaymentGateways.Edit);

        group.MapPost("{gatewayKey}/disable", async Task<IResult> (string gatewayKey, ISender sender) =>
            MapResult(await sender.Send(new SetPaymentGatewayEnabledCommand(gatewayKey, false))))
            .RequirePermission(PermissionConstants.PaymentGateways.Edit);

        group.MapPost("{gatewayKey}/test-connection", async Task<IResult> (string gatewayKey, ISender sender) =>
            MapResult(await sender.Send(new TestPaymentGatewayConnectionCommand(gatewayKey))))
            .RequirePermission(PermissionConstants.PaymentGateways.Edit);
    }

    private static IResult MapResult(Result result) =>
        result.IsSuccess
            ? TypedResults.Ok()
            : TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description, Type = result.Error.Code });

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description, Type = result.Error.Code });
}
