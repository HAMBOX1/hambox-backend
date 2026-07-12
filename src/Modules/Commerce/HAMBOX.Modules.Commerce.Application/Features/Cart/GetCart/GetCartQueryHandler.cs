using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;

internal sealed class GetCartQueryHandler : IRequestHandler<GetCartQuery, Result<CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly CartResponseBuilder _cartResponseBuilder;

    public GetCartQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService,
        CartResponseBuilder cartResponseBuilder)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
        _cartResponseBuilder = cartResponseBuilder;
    }

    public async Task<Result<CartDto>> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null)
        {
            return Result.Success(CommerceMapper.ToEmptyCartDto(request.GuestSessionId));
        }

        var dto = await _cartResponseBuilder.BuildAsync(
            cart,
            request.GuestSessionId ?? cart.GuestSessionId,
            countryCode: null,
            cancellationToken);

        return Result.Success(dto);
    }
}
