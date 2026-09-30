using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.TotalsPreview;

/// <summary>
/// Previews cart totals as they would be if checked out through the given payment gateway — lets the
/// checkout page show the gateway's own fee before the customer commits, mirroring
/// <c>GetMembershipCheckoutPreviewQuery</c>'s "preview" shape.
/// </summary>
public sealed record GetCheckoutTotalsPreviewQuery(string? GuestSessionId, string PaymentMethod, string? Country)
    : IRequest<Result<CartTotalsDto>>;

internal sealed class GetCheckoutTotalsPreviewQueryHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUserService,
    CartResponseBuilder cartResponseBuilder) : IRequestHandler<GetCheckoutTotalsPreviewQuery, Result<CartTotalsDto>>
{
    public async Task<Result<CartTotalsDto>> Handle(GetCheckoutTotalsPreviewQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            commerceDbContext,
            currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null)
        {
            return Result.Success(new CartTotalsDto(0m, 0m, 0m, 0m, 0, [], [], null));
        }

        var (subtotal, discountAmount, taxAmount, totalAmount, evaluation) =
            await cartResponseBuilder.BuildOrderAmountsAsync(cart, request.Country, cancellationToken, request.PaymentMethod);

        return Result.Success(new CartTotalsDto(
            subtotal,
            discountAmount,
            taxAmount,
            totalAmount,
            evaluation.ItemCount,
            evaluation.AppliedPromotions
                .Select(p => new AppliedPromotionDto(
                    p.PromotionId,
                    p.Name,
                    p.Type.ToString(),
                    p.CouponCode,
                    p.DiscountAmount,
                    p.IsAutomatic,
                    p.Description))
                .ToList(),
            evaluation.ValidationErrors,
            cart.AppliedCouponCode));
    }
}
