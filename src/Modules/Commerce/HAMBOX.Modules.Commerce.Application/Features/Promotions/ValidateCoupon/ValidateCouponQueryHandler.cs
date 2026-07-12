using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.ValidateCoupon;

public sealed record ValidateCouponQuery(ValidateCouponRequest Request, string? GuestSessionId)
    : IRequest<Result<ValidateCouponResponse>>;

internal sealed class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, Result<ValidateCouponResponse>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPromotionEngine _promotionEngine;
    private readonly IMembershipEngine _membershipEngine;

    public ValidateCouponQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        IPromotionEngine promotionEngine,
        IMembershipEngine membershipEngine)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _promotionEngine = promotionEngine;
        _membershipEngine = membershipEngine;
    }

    public async Task<Result<ValidateCouponResponse>> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Success(new ValidateCouponResponse(false, "Cart is empty.", null, null));
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
            request.Request.CountryCode,
            cancellationToken);

        var validation = await _promotionEngine.ValidateCouponAsync(request.Request.CouponCode, context, cancellationToken);
        return Result.Success(new ValidateCouponResponse(
            validation.IsValid,
            validation.ErrorMessage,
            validation.PromotionId,
            validation.CouponCodeId));
    }
}
