using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.ApplyCartCoupon;

public sealed class ApplyCartCouponCommandValidator : AbstractValidator<ApplyCartCouponCommand>
{
    private const string CouponCodePattern = "^[A-Za-z0-9_-]+$";

    public ApplyCartCouponCommandValidator()
    {
        RuleFor(x => x.CouponCode)
            .NotEmpty()
            .Length(3, 50)
            .Matches(CouponCodePattern)
            .WithMessage("Coupon code may only contain letters, numbers, hyphens, and underscores.");
    }
}
