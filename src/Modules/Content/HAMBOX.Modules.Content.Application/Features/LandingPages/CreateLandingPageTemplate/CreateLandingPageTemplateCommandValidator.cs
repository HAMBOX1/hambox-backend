using FluentValidation;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;

public sealed class CreateLandingPageTemplateCommandValidator : AbstractValidator<CreateLandingPageTemplateCommand>
{
    public CreateLandingPageTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
