using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.GenerateCoupons;

public sealed class GenerateCouponsCommandValidator : AbstractValidator<GenerateCouponsCommand>
{
    public GenerateCouponsCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();

        RuleFor(x => x.Request.Count).InclusiveBetween(1, 500);
        RuleFor(x => x.Request.Prefix).MaximumLength(20);
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
