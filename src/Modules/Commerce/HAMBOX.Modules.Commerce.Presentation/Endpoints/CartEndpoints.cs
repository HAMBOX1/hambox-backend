using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.ClearCart;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using HAMBOX.Modules.Commerce.Application.Features.Cart.MergeCart;
using HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.UpdateCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Features.Orders.GetOrderById;
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
            var result = await sender.Send(new AddCartItemCommand(request.ProductId, request.Quantity, guestCartId));

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
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateCartItemCommand(productId, request.Quantity, guestCartId));

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
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveCartItemCommand(productId, guestCartId));

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
        .RequireAuthorization();

        group.MapPost("checkout", async Task<Results<Ok<OrderDto>, BadRequest<ProblemDetails>>> (
            [FromBody] CheckoutRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new CheckoutCommand(request.Email, request.Country, request.PaymentMethod));

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
        .RequireAuthorization();

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
        .RequireAuthorization();
    }
}

internal sealed record CartItemRequest(Guid ProductId, int Quantity);
internal sealed record UpdateCartItemRequest(int Quantity);
internal sealed record MergeCartRequest(string GuestSessionId);
internal sealed record CheckoutRequest(string Email, string Country, string PaymentMethod);
