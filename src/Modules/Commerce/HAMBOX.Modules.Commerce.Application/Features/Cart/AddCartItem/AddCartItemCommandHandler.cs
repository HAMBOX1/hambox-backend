using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Fulfillment;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;

internal sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly IFulfillmentRouter _fulfillmentRouter;
    private readonly CartResponseBuilder _cartResponseBuilder;
    private readonly IMembershipAccessProvider _membershipAccess;

    public AddCartItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        IInventoryEngine inventoryEngine,
        IFulfillmentRouter fulfillmentRouter,
        CartResponseBuilder cartResponseBuilder,
        IMembershipAccessProvider membershipAccess)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _inventoryEngine = inventoryEngine;
        _fulfillmentRouter = fulfillmentRouter;
        _cartResponseBuilder = cartResponseBuilder;
        _membershipAccess = membershipAccess;
    }

    public async Task<Result<Contracts.CartDto>> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
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

        var membership = await _membershipAccess.GetAccessInfoAsync(_currentUserService.UserId, cancellationToken);

        if (product.PublicReleaseOnUtc is DateTime releaseOnUtc && releaseOnUtc > DateTime.UtcNow)
        {
            var earlyAccessStartsUtc = releaseOnUtc.AddDays(-membership.EarlyAccessDays);
            if (DateTime.UtcNow < earlyAccessStartsUtc)
            {
                return Result.Failure<Contracts.CartDto>(CommerceErrors.ProductNotYetReleased(releaseOnUtc));
            }
        }

        var productAccess = await _membershipAccess.GetProductAccessAsync(_currentUserService.UserId, product.Id, cancellationToken);
        if (productAccess is { IsRestricted: true, HasAccess: false })
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.ProductMembersOnly(productAccess.RequiredPlanNames));
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
        }
        else if (await _inventoryEngine.ProductHasVariantsAsync(request.ProductId, cancellationToken))
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.VariantRequired);
        }
        else
        {
            // No variant was requested and the product has no active, visible variant at all —
            // there is no inventory-backed way to ever deliver this product. Reject rather than
            // falling back to the legacy Product.StockQuantity counter, which is CSV-import
            // bookkeeping only and has no real digital code behind it.
            return Result.Failure<Contracts.CartDto>(CatalogErrors.ProductNotPurchasable);
        }

        var (cart, guestSessionId) = await CartResolver.GetOrCreateCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            createGuestSessionIfMissing: true,
            cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i =>
            i.ProductId == request.ProductId && i.ProductVariantId == request.ProductVariantId);
        var newQuantity = existingItem is null
            ? request.Quantity
            : existingItem.Quantity + request.Quantity;

        // A ProductVariantId is guaranteed at this point: the block above already rejected
        // any request that didn't resolve to one, so there is no variant-less stock branch here.
        // Same combined manual/supplier-readiness rule CartLineValidator uses at checkout, so a
        // supplier-backed variant with zero manual codes isn't rejected here only to be accepted
        // later at checkout (or vice versa).
        var stock = await _inventoryEngine.GetVariantStockAsync(request.ProductVariantId!.Value, cancellationToken);
        var readiness = await _fulfillmentRouter.GetReadinessAsync(request.ProductVariantId!.Value, cancellationToken);
        if (!FulfillmentAvailability.IsAvailable(readiness.Mode, stock.Available >= newQuantity, readiness.SupplierReady))
        {
            return Result.Failure<Contracts.CartDto>(
                CatalogErrors.InsufficientInventoryQuantity(
                    stock.Available,
                    newQuantity,
                    existingItem?.Quantity ?? 0));
        }

        cart.AddOrUpdateItem(request.ProductId, newQuantity, unitPrice, request.ProductVariantId);

        await CartPersistenceHelper.PrepareForSaveAsync(_commerceDbContext, cart, cancellationToken);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var dto = await _cartResponseBuilder.BuildAsync(cart, guestSessionId, countryCode: null, cancellationToken);
        return Result.Success(dto);
    }
}
