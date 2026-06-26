using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.GetWishlist;

public sealed record GetWishlistQuery : IRequest<Result<IReadOnlyList<Contracts.Account.WishlistItemDto>>>;

internal sealed class GetWishlistQueryHandler : IRequestHandler<GetWishlistQuery, Result<IReadOnlyList<Contracts.Account.WishlistItemDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetWishlistQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<Contracts.Account.WishlistItemDto>>> Handle(
        GetWishlistQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<IReadOnlyList<Contracts.Account.WishlistItemDto>>(CommerceErrors.AuthenticationRequired);
        }

        var items = await _commerceDbContext.WishlistItems
            .Where(w => w.UserId == _currentUserService.UserId)
            .OrderByDescending(w => w.AddedOnUtc)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Result.Success<IReadOnlyList<Contracts.Account.WishlistItemDto>>(Array.Empty<Contracts.Account.WishlistItemDto>());
        }

        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            productIds,
            cancellationToken);

        var dtos = items
            .Select(item =>
            {
                products.TryGetValue(item.ProductId, out var product);
                imageUrls.TryGetValue(item.ProductId, out var imageUrl);
                return AccountMapper.ToWishlistItemDto(
                    item,
                    product?.NameEn ?? "Unknown Product",
                    product?.Price ?? 0m,
                    imageUrl);
            })
            .ToList();

        return Result.Success<IReadOnlyList<Contracts.Account.WishlistItemDto>>(dtos);
    }
}
