using FluentValidation;

namespace HAMBOX.Modules.Themes.Application.Features.Campaigns;

public sealed class CreateCampaignCommandValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(2000);
        RuleFor(x => x.Request.ThemeId).NotEmpty();
        RuleFor(x => x.Request.StartsAtUtc.Kind).Equal(DateTimeKind.Utc)
            .WithMessage("StartsAtUtc must be a UTC date/time.");
        RuleFor(x => x.Request.EndsAtUtc.Kind).Equal(DateTimeKind.Utc)
            .WithMessage("EndsAtUtc must be a UTC date/time.");
        RuleFor(x => x.Request.EndsAtUtc).GreaterThan(x => x.Request.StartsAtUtc)
            .WithMessage("EndsAtUtc must be after StartsAtUtc.");
        RuleFor(x => x.Request.Priority).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCampaignCommandValidator : AbstractValidator<UpdateCampaignCommand>
{
    public UpdateCampaignCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(2000);
        RuleFor(x => x.Request.ThemeId).NotEmpty();
        RuleFor(x => x.Request.StartsAtUtc.Kind).Equal(DateTimeKind.Utc)
            .WithMessage("StartsAtUtc must be a UTC date/time.");
        RuleFor(x => x.Request.EndsAtUtc.Kind).Equal(DateTimeKind.Utc)
            .WithMessage("EndsAtUtc must be a UTC date/time.");
        RuleFor(x => x.Request.EndsAtUtc).GreaterThan(x => x.Request.StartsAtUtc)
            .WithMessage("EndsAtUtc must be after StartsAtUtc.");
        RuleFor(x => x.Request.Priority).GreaterThanOrEqualTo(0);
    }
}
