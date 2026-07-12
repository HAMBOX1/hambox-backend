using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.CreateCoupon;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.CreatePromotion;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.DeletePromotion;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.DuplicatePromotion;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.GenerateCoupons;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotionById;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotionRedemptions;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotions;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotionStatistics;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.SetPromotionStatus;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.UpdatePromotion;
using HAMBOX.Modules.Commerce.Application.Features.Promotions.ValidateCoupon;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/promotions")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Promotions")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            [FromQuery] string? type,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            var result = await sender.Send(new GetPromotionsQuery(pageNumber, pageSize, searchTerm, status, type));
            return MapResult(result);
        })
        .WithName("GetPromotions")
        .RequirePermission(PermissionConstants.Promotions.View);

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPromotionByIdQuery(id));
            return MapResult(result);
        })
        .WithName("GetPromotionById")
        .RequirePermission(PermissionConstants.Promotions.View);

        group.MapPost("", async Task<IResult> ([FromBody] CreatePromotionRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreatePromotionCommand(request));
            return MapResult(result, StatusCodes.Status201Created);
        })
        .WithName("CreatePromotion")
        .RequirePermission(PermissionConstants.Promotions.Create);

        group.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdatePromotionRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdatePromotionCommand(id, request));
            return MapResult(result);
        })
        .WithName("UpdatePromotion")
        .RequirePermission(PermissionConstants.Promotions.Edit);

        group.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePromotionCommand(id));
            return MapResult(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeletePromotion")
        .RequirePermission(PermissionConstants.Promotions.Delete);

        group.MapPost("{id:guid}/duplicate", async Task<IResult> (
            Guid id,
            [FromBody] DuplicatePromotionRequest? request,
            ISender sender) =>
        {
            var result = await sender.Send(new DuplicatePromotionCommand(id, request?.NewName));
            return MapResult(result, StatusCodes.Status201Created);
        })
        .WithName("DuplicatePromotion")
        .RequirePermission(PermissionConstants.Promotions.Create);

        group.MapPost("{id:guid}/publish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SetPromotionStatusCommand(id, PromotionStatusAction.Publish))))
        .WithName("PublishPromotion")
        .RequirePermission(PermissionConstants.Promotions.Publish);

        group.MapPost("{id:guid}/activate", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SetPromotionStatusCommand(id, PromotionStatusAction.Activate))))
        .WithName("ActivatePromotion")
        .RequirePermission(PermissionConstants.Promotions.Publish);

        group.MapPost("{id:guid}/deactivate", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SetPromotionStatusCommand(id, PromotionStatusAction.Deactivate))))
        .WithName("DeactivatePromotion")
        .RequirePermission(PermissionConstants.Promotions.Publish);

        group.MapPost("{id:guid}/archive", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SetPromotionStatusCommand(id, PromotionStatusAction.Archive))))
        .WithName("ArchivePromotion")
        .RequirePermission(PermissionConstants.Promotions.Edit);

        group.MapGet("{id:guid}/statistics", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetPromotionStatisticsQuery(id))))
        .WithName("GetPromotionStatistics")
        .RequirePermission(PermissionConstants.Promotions.View);

        group.MapGet("{id:guid}/redemptions", async Task<IResult> (
            Guid id,
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            return MapResult(await sender.Send(new GetPromotionRedemptionsQuery(id, pageNumber, pageSize)));
        })
        .WithName("GetPromotionRedemptions")
        .RequirePermission(PermissionConstants.Promotions.View);

        group.MapPost("{id:guid}/coupons", async Task<IResult> (
            Guid id,
            [FromBody] CreateCouponRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new CreateCouponCommand(id, request)), StatusCodes.Status201Created))
        .WithName("CreatePromotionCoupon")
        .RequirePermission(PermissionConstants.Coupons.Create);

        group.MapPost("{id:guid}/coupons/generate", async Task<IResult> (
            Guid id,
            [FromBody] GenerateCouponsRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new GenerateCouponsCommand(id, request)), StatusCodes.Status201Created))
        .WithName("GeneratePromotionCoupons")
        .RequirePermission(PermissionConstants.Coupons.Generate);

        group.MapPost("validate", async Task<IResult> (
            [FromBody] ValidateCouponRequest request,
            [FromHeader(Name = "X-Guest-Cart-Id")] string? guestCartId,
            ISender sender) =>
            MapResult(await sender.Send(new ValidateCouponQuery(request, guestCartId))))
        .WithName("ValidateCoupon")
        .AllowAnonymous();
    }

    private static IResult MapResult(Result result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? Results.StatusCode(successStatus)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });

    private static IResult MapResult<T>(Result<T> result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? successStatus == StatusCodes.Status200OK
                ? Results.Ok(result.Value)
                : Results.Created(string.Empty, result.Value)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });
}

internal sealed record DuplicatePromotionRequest(string? NewName);
