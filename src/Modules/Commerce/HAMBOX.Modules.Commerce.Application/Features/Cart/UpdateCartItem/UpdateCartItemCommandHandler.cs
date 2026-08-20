using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.UpdateCartItem;

internal sealed class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly IFulfillmentRouter _fulfillmentRouter;
    private readonly CartResponseBuilder _cartResponseBuilder;

    public UpdateCartItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        IInventoryEngine inventoryEngine,
        IFulfillmentRouter fulfillmentRouter,
        CartResponseBuilder cartResponseBuilder)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _inventoryEngine = inventoryEngine;
        _fulfillmentRouter = fulfillmentRouter;
        _cartResponseBuilder = cartResponseBuilder;
    }

    public async Task<Result<Contracts.CartDto>> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null || cart.Items.All(i =>
                i.ProductId != request.ProductId || i.ProductVariantId != request.ProductVariantId))
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.CartItemNotFound);
        }

        var product = await _catalogDbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.ProductNotFound);
        }

        if (product.Status != ProductStatus.Active)
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.ProductNotActive);
        }

        decimal unitPrice = product.Price;

        if (request.ProductVariantId is Guid variantId)
        {
            var variant = await _catalogDbContext.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == request.ProductId, cancellationToken);

            if (variant is null || variant.Status != ProductVariantStatus.Active || !variant.IsVisible)
            {
                return Result.Failure<Contracts.CartDto>(CatalogErrors.VariantNotFound);
            }

            unitPrice = variant.PriceOverride ?? product.Price;

            var stock = await _inventoryEngine.GetVariantStockAsync(variantId, cancellationToken);
            var readiness = await _fulfillmentRouter.GetReadinessAsync(variantId, cancellationToken);
            if (!FulfillmentAvailability.IsAvailable(readiness.Mode, stock.Available >= request.Quantity, readiness.SupplierReady))
            {
                return Result.Failure<Contracts.CartDto>(
                    CatalogErrors.InsufficientInventoryQuantity(
                        stock.Available,
                        request.Quantity,
                        alreadyInCart: 0));
            }
        }
        else if (await _inventoryEngine.ProductHasVariantsAsync(request.ProductId, cancellationToken))
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.VariantRequired);
        }
        else if (product.AvailableStock < request.Quantity)
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.InsufficientStock);
        }

        cart.UpdateItemQuantity(request.ProductId, request.Quantity, unitPrice, request.ProductVariantId);

        await CartPersistenceHelper.PrepareForSaveAsync(_commerceDbContext, cart, cancellationToken);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var dto = await _cartResponseBuilder.BuildAsync(
            cart,
            request.GuestSessionId ?? cart.GuestSessionId,
            countryCode: null,
            cancellationToken);

        return Result.Success(dto);
    }
}
