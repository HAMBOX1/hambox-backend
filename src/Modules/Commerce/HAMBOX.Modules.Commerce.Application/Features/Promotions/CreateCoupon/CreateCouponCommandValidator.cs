using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.CreateCoupon;

public sealed class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    private const string CouponCodePattern = "^[A-Za-z0-9_-]+$";

    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();

        RuleFor(x => x.Request.Code)
            .NotEmpty()
            .Length(3, 50)
            .Matches(CouponCodePattern)
            .WithMessage("Coupon code may only contain letters, numbers, hyphens, and underscores.");

        RuleFor(x => x.Request.MaxUses).GreaterThan(0).When(x => x.Request.MaxUses.HasValue);
        RuleFor(x => x.Request.PerUserMaxUses).GreaterThan(0).When(x => x.Request.PerUserMaxUses.HasValue);

        RuleFor(x => x.Request.PerUserMaxUses)
            .LessThanOrEqualTo(x => x.Request.MaxUses!.Value)
            .WithMessage("Per-user usage limit cannot exceed the total usage limit.")
            .When(x => x.Request.PerUserMaxUses.HasValue && x.Request.MaxUses.HasValue);

        RuleFor(x => x.Request.MaxUses)
            .Equal(1)
            .WithMessage("A single-use coupon's maximum uses must be 1.")
            .When(x => x.Request.IsSingleUse && x.Request.MaxUses.HasValue);
    }
}
