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

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartItem;

internal sealed class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly CartResponseBuilder _cartResponseBuilder;

    public RemoveCartItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        CartResponseBuilder cartResponseBuilder)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _cartResponseBuilder = cartResponseBuilder;
    }

    public async Task<Result<Contracts.CartDto>> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
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

        cart.RemoveItem(request.ProductId, request.ProductVariantId);

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
