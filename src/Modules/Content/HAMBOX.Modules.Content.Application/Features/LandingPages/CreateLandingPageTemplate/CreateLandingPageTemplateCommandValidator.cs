using FluentValidation;
using HAMBOX.Modules.Content.Domain.LandingPages;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;

public sealed class CreateLandingPageTemplateCommandValidator : AbstractValidator<CreateLandingPageTemplateCommand>
{
    public CreateLandingPageTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        RuleFor(x => x.TargetId)
            .Null()
            .When(x => x.Scope == LandingPageScope.Homepage)
            .WithMessage("Homepage templates cannot have a target.");

        RuleFor(x => x.TargetId)
            .NotNull()
            .When(x => x.Scope != LandingPageScope.Homepage)
            .WithMessage("Product/Category templates require a target.");
    }
}
