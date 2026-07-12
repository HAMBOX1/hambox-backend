using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.ApplyCartCoupon;

public sealed record ApplyCartCouponCommand(string CouponCode, string? GuestSessionId, string? CountryCode)
    : IRequest<Result<CartDto>>;

internal sealed class ApplyCartCouponCommandHandler : IRequestHandler<ApplyCartCouponCommand, Result<CartDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPromotionEngine _promotionEngine;
    private readonly IMembershipEngine _membershipEngine;
    private readonly CartResponseBuilder _cartResponseBuilder;

    public ApplyCartCouponCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        IPromotionEngine promotionEngine,
        IMembershipEngine membershipEngine,
        CartResponseBuilder cartResponseBuilder)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _promotionEngine = promotionEngine;
        _membershipEngine = membershipEngine;
        _cartResponseBuilder = cartResponseBuilder;
    }

    public async Task<Result<CartDto>> Handle(ApplyCartCouponCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Failure<CartDto>(CommerceErrors.CartEmpty);
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var context = await PromotionContextFactory.CreateAsync(
            _commerceDbContext,
            _membershipEngine,
            cart,
            products,
            _currentUserService.IsAuthenticated,
            _currentUserService.UserId,
            request.CountryCode,
            cancellationToken);

        var validation = await _promotionEngine.ValidateCouponAsync(request.CouponCode, context, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CartDto>(CommerceErrors.InvalidCoupon(validation.ErrorMessage ?? "Invalid coupon."));
        }

        cart.ApplyCouponCode(request.CouponCode);
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
