using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.ApplyCartCoupon;
using HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartCoupon;
using HAMBOX.Modules.Commerce.Application.Features.Cart.ClearCart;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using HAMBOX.Modules.Commerce.Application.Features.Cart.MergeCart;
using HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.UpdateCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Membership;
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
/// Registers the shopping cart and checkout endpoints.
/// </summary>
internal static class CartEndpoints
{
    private const string GuestCartHeader = "X-Guest-Cart-Id";

    public static void MapCartEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Commerce")
            .HasApiVersion(1);

        group.MapGet("cart", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCartQuery(guestCartId));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("GetCart")
        .AllowAnonymous();

        group.MapPost("cart/items", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            [FromBody] CartItemRequest request,
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            ISender sender) =>
        {
            var result = await sender.Send(new AddCartItemCommand(
                request.ProductId, request.Quantity, guestCartId, request.ProductVariantId));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("AddCartItem")
        .AllowAnonymous();

        group.MapPut("cart/items/{productId:guid}", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            Guid productId,
            [FromBody] UpdateCartItemRequest request,
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            [FromQuery] Guid? variantId,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateCartItemCommand(productId, request.Quantity, guestCartId, variantId));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("UpdateCartItem")
        .AllowAnonymous();

        group.MapDelete("cart/items/{productId:guid}", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            Guid productId,
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            [FromQuery] Guid? variantId,
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveCartItemCommand(productId, guestCartId, variantId));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("RemoveCartItem")
        .AllowAnonymous();

        group.MapDelete("cart", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            ISender sender) =>
        {
            var result = await sender.Send(new ClearCartCommand(guestCartId));

            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("ClearCart")
        .AllowAnonymous();

        group.MapPost("cart/merge", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            [FromBody] MergeCartRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new MergeCartCommand(request.GuestSessionId));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("MergeCart")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapPost("cart/promotions/apply", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            [FromBody] ApplyCartCouponRequest request,
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            ISender sender) =>
        {
            var result = await sender.Send(new ApplyCartCouponCommand(request.CouponCode, guestCartId, request.Country));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("ApplyCartCoupon")
        .AllowAnonymous();

        group.MapDelete("cart/promotions", async Task<Results<Ok<CartDto>, BadRequest<ProblemDetails>>> (
            [FromHeader(Name = GuestCartHeader)] string? guestCartId,
            [FromQuery] string? country,
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveCartCouponCommand(guestCartId, country));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("RemoveCartCoupon")
        .AllowAnonymous();

        group.MapGet("checkout/configuration", async Task<Results<Ok<CheckoutConfigurationDto>, BadRequest<ProblemDetails>>> (
            ISender sender) =>
        {
            var result = await sender.Send(new GetCheckoutConfigurationQuery());

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("GetCheckoutConfiguration")
        .AllowAnonymous();

        group.MapPost("checkout", async Task<Results<Ok<OrderDto>, BadRequest<ProblemDetails>>> (
            [FromBody] CheckoutRequest request,
            HttpContext httpContext,
            ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() is { Length: > 0 } ua ? ua : "unknown";
            var language = httpContext.Request.Headers["Accept-Language"].ToString() is { Length: > 0 } acceptLanguage
                ? acceptLanguage.Split(',')[0].Split(';')[0].Trim()
                : "en";

            var result = await sender.Send(new CheckoutCommand(
                request.Email, request.Country, request.PaymentMethod, ipAddress, userAgent, language));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("Checkout")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapGet("checkout/membership/preview", async Task<Results<Ok<MembershipCheckoutPreviewDto>, BadRequest<ProblemDetails>>> (
            [FromQuery] Guid planId,
            [FromQuery] string action,
            [FromQuery] string? couponCode,
            ISender sender) =>
        {
            var result = await sender.Send(new GetMembershipCheckoutPreviewQuery(planId, action, couponCode));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("GetMembershipCheckoutPreview")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapPost("checkout/membership", async Task<Results<Ok<OrderDto>, BadRequest<ProblemDetails>>> (
            [FromBody] MembershipCheckoutRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new MembershipCheckoutCommand(
                request.PlanId,
                request.Action,
                request.Email,
                request.Country,
                request.PaymentMethod,
                request.CouponCode));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("MembershipCheckout")
        .RequireAuthorization()
        .RequireCustomerContext();

        group.MapGet("orders/{id:guid}", async Task<Results<Ok<OrderDetailDto>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> (
            Guid id,
            ISender sender) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(id));

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
        })
        .WithName("GetOrderById")
        .RequireAuthorization()
        .RequireCustomerContext();
    }
}

internal sealed record CartItemRequest(Guid ProductId, int Quantity, Guid? ProductVariantId = null);
internal sealed record UpdateCartItemRequest(int Quantity);
internal sealed record MergeCartRequest(string GuestSessionId);
internal sealed record ApplyCartCouponRequest(string CouponCode, string? Country);
internal sealed record CheckoutRequest(string Email, string Country, string PaymentMethod);
