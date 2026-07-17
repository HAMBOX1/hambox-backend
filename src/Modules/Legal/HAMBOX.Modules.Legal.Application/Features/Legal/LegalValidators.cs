using FluentValidation;

namespace HAMBOX.Modules.Legal.Application.Features.Legal;

internal sealed class CreateLegalSectionCommandValidator : AbstractValidator<CreateLegalSectionCommand>
{
    public CreateLegalSectionCommandValidator()
    {
        RuleFor(x => x.Request.Slug).NotEmpty().MaximumLength(150).Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
        RuleFor(x => x.Request.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.TitleAr).MaximumLength(200);
        RuleFor(x => x.Request.Category).MaximumLength(100);
        RuleFor(x => x.Request.Icon).MaximumLength(100);
    }
}

internal sealed class UpdateLegalSectionCommandValidator : AbstractValidator<UpdateLegalSectionCommand>
{
    public UpdateLegalSectionCommandValidator()
    {
        RuleFor(x => x.Request.Slug).NotEmpty().MaximumLength(150).Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
        RuleFor(x => x.Request.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.TitleAr).MaximumLength(200);
        RuleFor(x => x.Request.ContentEn).NotEmpty();
        RuleFor(x => x.Request.Category).MaximumLength(100);
        RuleFor(x => x.Request.Icon).MaximumLength(100);
        RuleFor(x => x.Request.DescriptionEn).MaximumLength(500);
        RuleFor(x => x.Request.DescriptionAr).MaximumLength(500);
        RuleFor(x => x.Request.SeoTitle).MaximumLength(200);
        RuleFor(x => x.Request.SeoDescription).MaximumLength(500);
        RuleFor(x => x.Request.SeoKeywords).MaximumLength(300);
        RuleFor(x => x.Request.VersionNotes).MaximumLength(1000);
        RuleFor(x => x.Request.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class DuplicateLegalSectionCommandValidator : AbstractValidator<DuplicateLegalSectionCommand>
{
    public DuplicateLegalSectionCommandValidator()
    {
        RuleFor(x => x.Request.NewSlug).NotEmpty().MaximumLength(150).Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
        RuleFor(x => x.Request.NewTitleEn).MaximumLength(200);
    }
}
