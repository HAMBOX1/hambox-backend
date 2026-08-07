using HAMBOX.Application.Abstractions;
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

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.MoveWishlistItemToCart;

public sealed record MoveWishlistItemToCartCommand(Guid ProductId) : IRequest<Result<Contracts.CartDto>>;

internal sealed class MoveWishlistItemToCartCommandHandler : IRequestHandler<MoveWishlistItemToCartCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly CartResponseBuilder _cartResponseBuilder;
    private readonly IMembershipAccessProvider _membershipAccess;

    public MoveWishlistItemToCartCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        IInventoryEngine inventoryEngine,
        CartResponseBuilder cartResponseBuilder,
        IMembershipAccessProvider membershipAccess)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _inventoryEngine = inventoryEngine;
        _cartResponseBuilder = cartResponseBuilder;
        _membershipAccess = membershipAccess;
    }

    public async Task<Result<Contracts.CartDto>> Handle(
        MoveWishlistItemToCartCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.AuthenticationRequired);
        }

        var wishlistItem = await _commerceDbContext.WishlistItems
            .FirstOrDefaultAsync(
                w => w.UserId == _currentUserService.UserId && w.ProductId == request.ProductId,
                cancellationToken);

        if (wishlistItem is null)
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.WishlistItemNotFound);
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
        Guid? productVariantId = null;

        if (await _inventoryEngine.ProductHasVariantsAsync(request.ProductId, cancellationToken))
        {
            var activeVariants = await _catalogDbContext.ProductVariants
                .Where(v =>
                    v.ProductId == request.ProductId
                    && !v.IsDeleted
                    && v.Status == ProductVariantStatus.Active
                    && v.IsVisible)
                .ToListAsync(cancellationToken);

            if (activeVariants.Count != 1)
            {
                return Result.Failure<Contracts.CartDto>(CatalogErrors.VariantRequired);
            }

            var variant = activeVariants[0];
            productVariantId = variant.Id;
            unitPrice = variant.PriceOverride ?? product.Price;
        }

        var (cart, guestSessionId) = await CartResolver.GetOrCreateCartAsync(
            _commerceDbContext,
            _currentUserService,
            guestSessionId: null,
            createGuestSessionIfMissing: false,
            cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i =>
            i.ProductId == request.ProductId && i.ProductVariantId == productVariantId);
        var newQuantity = existingItem is null ? 1 : existingItem.Quantity + 1;

        if (productVariantId is Guid stockVariantId)
        {
            var stock = await _inventoryEngine.GetVariantStockAsync(stockVariantId, cancellationToken);
            if (stock.Available < newQuantity)
            {
                return Result.Failure<Contracts.CartDto>(
                    CatalogErrors.InsufficientInventoryQuantity(
                        stock.Available,
                        newQuantity,
                        existingItem?.Quantity ?? 0));
            }
        }
        else if (product.AvailableStock < newQuantity)
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.InsufficientStock);
        }

        cart.AddOrUpdateItem(request.ProductId, newQuantity, unitPrice, productVariantId);
        _commerceDbContext.WishlistItems.Remove(wishlistItem);

        await CartPersistenceHelper.PrepareForSaveAsync(_commerceDbContext, cart, cancellationToken);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var dto = await _cartResponseBuilder.BuildAsync(cart, guestSessionId, countryCode: null, cancellationToken);
        return Result.Success(dto);
    }
}
