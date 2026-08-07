using FluentValidation;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Domain.Memberships;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Plans;

public sealed class CreateMembershipPlanCommandValidator : AbstractValidator<CreateMembershipPlanCommand>
{
    public CreateMembershipPlanCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Slug).NotEmpty().MaximumLength(100).Matches(MembershipPlanValidationRules.SlugPattern)
            .WithMessage("Slug may only contain lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Request.Description).MaximumLength(2000);
        RuleFor(x => x.Request.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.DurationDays).GreaterThan(0);
        RuleFor(x => x.Request.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.BadgeLabel).MaximumLength(100);
        RuleFor(x => x.Request.ThemeKey).MaximumLength(100);
        RuleForEach(x => x.Request.Benefits).SetValidator(new MembershipBenefitDtoValidator())
            .When(x => x.Request.Benefits is not null);
    }
}

public sealed class UpdateMembershipPlanCommandValidator : AbstractValidator<UpdateMembershipPlanCommand>
{
    public UpdateMembershipPlanCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Slug).NotEmpty().MaximumLength(100).Matches(MembershipPlanValidationRules.SlugPattern)
            .WithMessage("Slug may only contain lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Request.Description).MaximumLength(2000);
        RuleFor(x => x.Request.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.DurationDays).GreaterThan(0);
        RuleFor(x => x.Request.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.BadgeLabel).MaximumLength(100);
        RuleFor(x => x.Request.ThemeKey).MaximumLength(100);
        RuleForEach(x => x.Request.Benefits).SetValidator(new MembershipBenefitDtoValidator())
            .When(x => x.Request.Benefits is not null);
    }
}

internal sealed class MembershipBenefitDtoValidator : AbstractValidator<MembershipBenefitDto>
{
    public MembershipBenefitDtoValidator()
    {
        RuleFor(x => x.Type).NotEmpty()
            .Must(type => Enum.TryParse<MembershipBenefitType>(type, ignoreCase: true, out _))
            .WithMessage("Unknown benefit type.");
        RuleFor(x => x.Value).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal static class MembershipPlanValidationRules
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
}
