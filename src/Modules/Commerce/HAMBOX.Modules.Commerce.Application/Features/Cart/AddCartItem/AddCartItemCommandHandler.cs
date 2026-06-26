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

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;

internal sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, Result<Contracts.CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public AddCartItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
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

        var (cart, guestSessionId) = await CartResolver.GetOrCreateCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            createGuestSessionIfMissing: true,
            cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        var newQuantity = existingItem is null
            ? request.Quantity
            : existingItem.Quantity + request.Quantity;

        if (product.AvailableStock < newQuantity)
        {
            return Result.Failure<Contracts.CartDto>(CatalogErrors.InsufficientStock);
        }

        cart.AddOrUpdateItem(request.ProductId, newQuantity, product.Price);

        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return Result.Success(CommerceMapper.ToCartDto(
            cart,
            guestSessionId,
            products,
            _currentUserService.IsAuthenticated));
    }
}
