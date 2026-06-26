using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;

internal sealed class GetCartQueryHandler : IRequestHandler<GetCartQuery, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetCartQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Contracts.CartDto>> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null)
        {
            var emptyCart = ShoppingCart.CreateForGuest(request.GuestSessionId ?? string.Empty);
            return Result.Success(CommerceMapper.ToCartDto(
                emptyCart,
                request.GuestSessionId,
                new Dictionary<Guid, Catalog.Domain.Products.Product>(),
                _currentUserService.IsAuthenticated));
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = productIds.Count == 0
            ? []
            : await _catalogDbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

        var dto = CommerceMapper.ToCartDto(
            cart,
            request.GuestSessionId ?? cart.GuestSessionId,
            products,
            _currentUserService.IsAuthenticated);

        return Result.Success(dto);
    }
}
