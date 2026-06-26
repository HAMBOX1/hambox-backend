using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.AddWishlistItem;

public sealed record AddWishlistItemCommand(Guid ProductId) : IRequest<Result<Contracts.Account.WishlistItemDto>>;

internal sealed class AddWishlistItemCommandHandler : IRequestHandler<AddWishlistItemCommand, Result<Contracts.Account.WishlistItemDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public AddWishlistItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Contracts.Account.WishlistItemDto>> Handle(
        AddWishlistItemCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<Contracts.Account.WishlistItemDto>(CommerceErrors.AuthenticationRequired);
        }

        var product = await _catalogDbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<Contracts.Account.WishlistItemDto>(CommerceErrors.ProductNotFound);
        }

        if (product.Status != ProductStatus.Active)
        {
            return Result.Failure<Contracts.Account.WishlistItemDto>(CatalogErrors.ProductNotActive);
        }

        var exists = await _commerceDbContext.WishlistItems
            .AnyAsync(w => w.UserId == _currentUserService.UserId && w.ProductId == request.ProductId, cancellationToken);

        if (exists)
        {
            return Result.Failure<Contracts.Account.WishlistItemDto>(CommerceErrors.WishlistItemExists);
        }

        var item = WishlistItem.Create(_currentUserService.UserId, request.ProductId);
        _commerceDbContext.WishlistItems.Add(item);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            [request.ProductId],
            cancellationToken);
        imageUrls.TryGetValue(request.ProductId, out var imageUrl);

        return Result.Success(AccountMapper.ToWishlistItemDto(item, product.NameEn, product.Price, imageUrl));
    }
}
