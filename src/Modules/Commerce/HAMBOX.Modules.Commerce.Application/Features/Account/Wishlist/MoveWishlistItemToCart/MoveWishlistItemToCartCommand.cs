using HAMBOX.Application.Abstractions;
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

    public MoveWishlistItemToCartCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
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

        var (cart, guestSessionId) = await CartResolver.GetOrCreateCartAsync(
            _commerceDbContext,
            _currentUserService,
            guestSessionId: null,
            createGuestSessionIfMissing: false,
            cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        var newQuantity = existingItem is null ? 1 : existingItem.Quantity + 1;

        if (product.AvailableStock < newQuantity)
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.InsufficientStock);
        }

        cart.AddOrUpdateItem(request.ProductId, newQuantity, product.Price);
        _commerceDbContext.WishlistItems.Remove(wishlistItem);

        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return Result.Success(CommerceMapper.ToCartDto(cart, guestSessionId, products, isAuthenticated: true));
    }
}
