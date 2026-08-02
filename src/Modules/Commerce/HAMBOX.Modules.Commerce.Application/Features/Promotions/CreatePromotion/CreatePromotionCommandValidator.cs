using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.CreatePromotion;

public sealed class CreatePromotionCommandValidator : AbstractValidator<CreatePromotionCommand>
{
    public CreatePromotionCommandValidator(IPlatformSettingsProvider platformSettings)
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(1000);

        RuleFor(x => x.Request.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<PromotionType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a valid promotion type.");

        RuleFor(x => x.Request.DiscountType)
            .NotEmpty()
            .Must(type => Enum.TryParse<DiscountType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a valid discount type.");

        RuleFor(x => x.Request.DiscountValue).GreaterThan(0);

        RuleFor(x => x.Request.DiscountValue)
            .MustAsync(async (command, value, ct) =>
            {
                var promotionsSettings = await platformSettings.GetAsync<PromotionsSettingsPayload>(
                    PlatformSettingsCategoryKeys.Promotions, ct);
                return value <= Math.Min(100m, promotionsSettings.DefaultMaxDiscountPercent);
            })
            .WithMessage("Percentage discounts cannot exceed the configured maximum discount percentage.")
            .When(x => IsPercentage(x.Request.DiscountType));

        RuleFor(x => x.Request.EndDateUtc)
            .GreaterThanOrEqualTo(x => x.Request.StartDateUtc!.Value)
            .WithMessage("End date must be on or after the start date.")
            .When(x => x.Request.StartDateUtc.HasValue && x.Request.EndDateUtc.HasValue);

        RuleFor(x => x.Request.InitialCouponCode).MaximumLength(50);

        RuleForEach(x => x.Request.Conditions).SetValidator(new PromotionConditionDtoValidator());
        RuleForEach(x => x.Request.Targets).SetValidator(new PromotionTargetDtoValidator());

        RuleFor(x => x.Request.Conditions)
            .Must(PromotionListRules.HaveNoDuplicateConditionTypes)
            .WithMessage("Each condition type can only be specified once per promotion.");

        RuleFor(x => x.Request.Targets)
            .Must(PromotionListRules.HaveNoDuplicateTargets)
            .WithMessage("Duplicate targets are not allowed on the same promotion.");
    }

    private static bool IsPercentage(string discountType) =>
        Enum.TryParse<DiscountType>(discountType, ignoreCase: true, out var parsed) && parsed == DiscountType.Percentage;
}
