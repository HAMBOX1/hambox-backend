using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartItem;

internal sealed class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public RemoveCartItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Contracts.CartDto>> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null || cart.Items.All(i => i.ProductId != request.ProductId))
        {
            return Result.Failure<Contracts.CartDto>(CommerceErrors.CartItemNotFound);
        }

        cart.RemoveItem(request.ProductId);

        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = productIds.Count == 0
            ? []
            : await _catalogDbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

        return Result.Success(CommerceMapper.ToCartDto(
            cart,
            request.GuestSessionId ?? cart.GuestSessionId,
            products,
            _currentUserService.IsAuthenticated));
    }
}
