using FluentValidation;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Shared FluentValidation rules for the <see cref="PromotionConditionDto"/>/<see cref="PromotionTargetDto"/>
/// lists nested inside CreatePromotion/UpdatePromotion requests.
/// </summary>
public sealed class PromotionConditionDtoValidator : AbstractValidator<PromotionConditionDto>
{
    public PromotionConditionDtoValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<PromotionConditionType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a valid condition type.");

        RuleFor(x => x.Value).NotEmpty();

        RuleFor(x => x)
            .Must(HaveValidValueForType)
            .WithMessage("Condition value is invalid for the specified condition type.")
            .When(x => Enum.TryParse<PromotionConditionType>(x.Type, ignoreCase: true, out _));
    }

    private static bool HaveValidValueForType(PromotionConditionDto dto)
    {
        Enum.TryParse<PromotionConditionType>(dto.Type, ignoreCase: true, out var type);

        return type switch
        {
            PromotionConditionType.MinOrderAmount =>
                decimal.TryParse(dto.Value, out var minOrder) && minOrder >= 0,
            PromotionConditionType.MaxDiscountAmount =>
                decimal.TryParse(dto.Value, out var maxDiscount) && maxDiscount > 0,
            PromotionConditionType.UsageLimit or PromotionConditionType.PerUserLimit =>
                int.TryParse(dto.Value, out var limit) && limit >= 1,
            _ => !string.IsNullOrWhiteSpace(dto.Value),
        };
    }
}

public sealed class PromotionTargetDtoValidator : AbstractValidator<PromotionTargetDto>
{
    public PromotionTargetDtoValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<PromotionTargetType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a valid target type.");

        RuleFor(x => x.TargetId).NotEmpty();
    }
}

/// <summary>
/// Collection-level duplicate checks shared by CreatePromotion/UpdatePromotion validators —
/// a promotion having the same condition type (or target) listed twice is ambiguous, since
/// <see cref="Promotion.GetConditionDecimal"/>/<see cref="Promotion.GetConditionInt"/> silently
/// use the first match and ignore the rest.
/// </summary>
public static class PromotionListRules
{
    public static bool HaveNoDuplicateConditionTypes(IReadOnlyList<PromotionConditionDto>? conditions) =>
        conditions is null ||
        conditions
            .GroupBy(c => c.Type.Trim().ToUpperInvariant())
            .All(g => g.Count() == 1);

    public static bool HaveNoDuplicateTargets(IReadOnlyList<PromotionTargetDto>? targets) =>
        targets is null ||
        targets
            .GroupBy(t => (Type: t.Type.Trim().ToUpperInvariant(), t.TargetId))
            .All(g => g.Count() == 1);
}
