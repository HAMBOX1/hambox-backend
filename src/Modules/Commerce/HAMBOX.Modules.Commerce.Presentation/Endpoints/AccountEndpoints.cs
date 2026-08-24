using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Features.Account.Dashboard.GetAccountDashboard;
using HAMBOX.Modules.Commerce.Application.Features.Account.Library;
using HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.ArchiveNotification;
using HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.DeleteNotification;
using HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.GetNotifications;
using HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.GetUnreadNotificationCount;
using HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.MarkAllNotificationsRead;
using HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.MarkNotificationRead;
using HAMBOX.Modules.Commerce.Application.Features.Account.Orders.GetOrders;
using HAMBOX.Modules.Commerce.Application.Features.Account.Referral.GetReferralDashboard;
using HAMBOX.Modules.Commerce.Application.Features.Account.Referral.GetReferralHistory;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.CreateReview;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.DeleteReview;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.GetMyReviews;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.GetProductReviews;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.UpdateReview;
using HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.AddWishlistItem;
using HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.GetWishlist;
using HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.MoveWishlistItemToCart;
using HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.RemoveWishlistItem;
using HAMBOX.Modules.Commerce.Application.Features.Memberships.Account;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Features.Orders.GetOrderById;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Registers customer account endpoints.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/account")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Account")
            .HasApiVersion(1)
            .RequireAuthorization()
            .RequireCustomerContext();

        group.MapGet("dashboard", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAccountDashboardQuery());
            return ToResult(result);
        }).WithName("GetAccountDashboard");

        group.MapGet("wishlist", async (ISender sender) =>
        {
            var result = await sender.Send(new GetWishlistQuery());
            return ToResult(result);
        }).WithName("GetWishlist");

        group.MapPost("wishlist", async ([FromBody] WishlistItemRequest request, ISender sender) =>
        {
            var result = await sender.Send(new AddWishlistItemCommand(request.ProductId));
            return ToResult(result);
        }).WithName("AddWishlistItem");

        group.MapDelete("wishlist/{productId:guid}", async (Guid productId, ISender sender) =>
        {
            var result = await sender.Send(new RemoveWishlistItemCommand(productId));
            return ToResult(result);
        }).WithName("RemoveWishlistItem");

        group.MapPost("wishlist/{productId:guid}/move-to-cart", async (Guid productId, ISender sender) =>
        {
            var result = await sender.Send(new MoveWishlistItemToCartCommand(productId));
            return ToResult(result);
        }).WithName("MoveWishlistItemToCart");

        group.MapGet("orders", async (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            ISender sender) =>
        {
            var result = await sender.Send(new GetOrdersQuery(pageNumber <= 0 ? 1 : pageNumber, pageSize <= 0 ? 10 : pageSize));
            return ToResult(result);
        }).WithName("GetOrders");

        group.MapGet("orders/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(id));
            return ToOrderResult(result);
        }).WithName("GetAccountOrderById");

        group.MapGet("library", async (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? deliveryStatus,
            [FromQuery] string? sort,
            [FromQuery] string? group,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerLibraryQuery(
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 20 : pageSize,
                searchTerm,
                deliveryStatus,
                sort,
                group));
            return ToResult(result);
        }).WithName("GetCustomerLibrary");

        group.MapGet("library/{id:guid}/reveal", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new RevealCustomerLibraryKeyQuery(id));
            return ToResult(result);
        }).WithName("RevealCustomerLibraryKey");

        group.MapGet("library/{orderItemId:guid}/instructions", async (Guid orderItemId, ISender sender) =>
        {
            var result = await sender.Send(new GetLibraryItemInstructionsQuery(orderItemId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Forbid();
        }).WithName("GetLibraryItemInstructions");

        group.MapGet("products/{productId:guid}/reviews", async (Guid productId, ISender sender) =>
        {
            var result = await sender.Send(new GetProductReviewsQuery(productId));
            return ToResult(result);
        }).WithName("GetProductReviews").AllowAnonymous();

        group.MapGet("reviews/mine", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMyReviewsQuery());
            return ToResult(result);
        }).WithName("GetMyReviews");

        group.MapPost("reviews", async ([FromBody] CreateReviewRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateReviewCommand(
                request.ProductId,
                request.OrderId,
                request.Rating,
                request.Comment));
            return ToResult(result);
        }).WithName("CreateReview");

        group.MapPut("reviews/{id:guid}", async (Guid id, [FromBody] UpdateReviewRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateReviewCommand(id, request.Rating, request.Comment));
            return ToResult(result);
        }).WithName("UpdateReview");

        group.MapDelete("reviews/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteReviewCommand(id));
            return ToResult(result);
        }).WithName("DeleteReview");

        group.MapGet("notifications", async (
            ISender sender,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeArchived = false,
            [FromQuery] bool? isRead = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null) =>
        {
            var result = await sender.Send(new GetNotificationsQuery(
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 20 : pageSize,
                includeArchived,
                isRead,
                category,
                search));
            return ToResult(result);
        }).WithName("GetNotifications");

        group.MapGet("notifications/unread-count", async (ISender sender) =>
        {
            var result = await sender.Send(new GetUnreadNotificationCountQuery());
            return ToResult(result);
        }).WithName("GetUnreadNotificationCount");

        group.MapPatch("notifications/{id:guid}/read", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new MarkNotificationReadCommand(id));
            return ToResult(result);
        }).WithName("MarkNotificationRead");

        group.MapPost("notifications/read-all", async (ISender sender) =>
        {
            var result = await sender.Send(new MarkAllNotificationsReadCommand());
            return ToResult(result);
        }).WithName("MarkAllNotificationsRead");

        group.MapPost("notifications/{id:guid}/archive", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new ArchiveNotificationCommand(id));
            return ToResult(result);
        }).WithName("ArchiveNotification");

        group.MapDelete("notifications/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteNotificationCommand(id));
            return ToResult(result);
        }).WithName("DeleteNotification");

        group.MapGet("referral", async (ISender sender) =>
        {
            var result = await sender.Send(new GetReferralDashboardQuery());
            return ToResult(result);
        }).WithName("GetReferralDashboard");

        group.MapGet("referral/history", async (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? status,
            ISender sender) =>
        {
            var result = await sender.Send(new GetReferralHistoryQuery(
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 10 : pageSize,
                search,
                status));
            return ToResult(result);
        }).WithName("GetReferralHistory");

        group.MapGet("membership", async (ISender sender) =>
            ToResult(await sender.Send(new GetCurrentMembershipQuery())))
        .WithName("GetCurrentMembership");

        group.MapGet("membership/history", async (ISender sender) =>
            ToResult(await sender.Send(new GetMembershipHistoryQuery())))
        .WithName("GetMembershipHistory");

        group.MapGet("membership/transactions", async (ISender sender) =>
            ToResult(await sender.Send(new GetMembershipTransactionsQuery())))
        .WithName("GetMembershipTransactions");

        group.MapPost("membership/upgrade", async ([FromBody] ChangeMembershipPlanRequest request, ISender sender) =>
            ToResult(await sender.Send(new CustomerUpgradeMembershipCommand(request))))
        .WithName("CustomerUpgradeMembership");

        group.MapPost("membership/downgrade", async ([FromBody] ChangeMembershipPlanRequest request, ISender sender) =>
            ToResult(await sender.Send(new CustomerDowngradeMembershipCommand(request))))
        .WithName("CustomerDowngradeMembership");

        group.MapPost("membership/renew", async (ISender sender) =>
            ToResult(await sender.Send(new CustomerRenewMembershipCommand())))
        .WithName("CustomerRenewMembership");

        group.MapPost("membership/subscribe", async ([FromBody] ChangeMembershipPlanRequest request, ISender sender) =>
            ToResult(await sender.Send(new CustomerSubscribeMembershipCommand(request))))
        .WithName("CustomerSubscribeMembership");
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

    private static Results<Ok<OrderDetailDto>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>> ToOrderResult(
        Result<OrderDetailDto> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        if (result.Error.Code is "Orders.NotFound")
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status404NotFound
            });
        }

        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Bad Request",
            Detail = result.Error.Description,
            Type = result.Error.Code,
            Status = StatusCodes.Status400BadRequest
        });
    }
}

internal sealed record WishlistItemRequest(Guid ProductId);
internal sealed record CreateReviewRequest(Guid ProductId, Guid OrderId, int Rating, string Comment);
internal sealed record UpdateReviewRequest(int Rating, string Comment);
