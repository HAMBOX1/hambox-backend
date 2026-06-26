using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.MergeCart;

internal sealed class MergeCartCommandHandler : IRequestHandler<MergeCartCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public MergeCartCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Contracts.CartDto>> Handle(MergeCartCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.AuthenticationRequired);
        }

        var guestCart = await _commerceDbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.GuestSessionId == request.GuestSessionId, cancellationToken);

        var (userCart, _) = await CartResolver.GetOrCreateCartAsync(
            _commerceDbContext,
            _currentUserService,
            guestSessionId: null,
            createGuestSessionIfMissing: true,
            cancellationToken);

        if (guestCart is not null && guestCart.Items.Count > 0)
        {
            userCart.MergeFrom(guestCart);
            _commerceDbContext.ShoppingCarts.Remove(guestCart);
        }

        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var productIds = userCart.Items.Select(i => i.ProductId).ToList();
        var products = productIds.Count == 0
            ? []
            : await _catalogDbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

        return Result.Success(CommerceMapper.ToCartDto(
            userCart,
            guestSessionId: null,
            products,
            isAuthenticated: true));
    }
}
