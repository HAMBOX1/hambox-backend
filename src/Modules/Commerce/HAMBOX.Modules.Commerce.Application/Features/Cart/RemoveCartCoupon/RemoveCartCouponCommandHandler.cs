using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartCoupon;

public sealed record RemoveCartCouponCommand(string? GuestSessionId, string? CountryCode)
    : IRequest<Result<CartDto>>;

internal sealed class RemoveCartCouponCommandHandler : IRequestHandler<RemoveCartCouponCommand, Result<CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly CartResponseBuilder _cartResponseBuilder;

    public RemoveCartCouponCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService,
        CartResponseBuilder cartResponseBuilder)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
        _cartResponseBuilder = cartResponseBuilder;
    }

    public async Task<Result<CartDto>> Handle(RemoveCartCouponCommand request, CancellationToken cancellationToken)
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

        cart.RemoveCouponCode();
        await CartPersistenceHelper.PrepareForSaveAsync(_commerceDbContext, cart, cancellationToken);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var dto = await _cartResponseBuilder.BuildAsync(
            cart,
            request.GuestSessionId ?? cart.GuestSessionId,
            request.CountryCode,
            cancellationToken);

        return Result.Success(dto);
    }
}
