using System.Text.Json;
using FluentValidation;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.SaveLandingPageTemplateDraft;

public sealed class SaveLandingPageTemplateDraftCommandValidator : AbstractValidator<SaveLandingPageTemplateDraftCommand>
{
    public SaveLandingPageTemplateDraftCommandValidator()
    {
        RuleFor(x => x.Sections).NotNull();

        RuleFor(x => x.Sections)
            .Must(sections => sections.Select(s => s.InstanceId).Distinct().Count() == sections.Count)
            .WithMessage("Section InstanceId values must be unique.")
            .When(x => x.Sections is not null);

        RuleForEach(x => x.Sections).ChildRules(section =>
        {
            section.RuleFor(s => s.Category).NotEmpty().MaximumLength(100);
            section.RuleFor(s => s.VariantKey).NotEmpty().MaximumLength(100);
            section.RuleFor(s => s.SortOrder).GreaterThanOrEqualTo(0);
            section.RuleFor(s => s.ConfigJson).Must(BeValidJson).WithMessage("ConfigJson must be valid JSON.");
        });
    }

    // The backend is deliberately variant-agnostic (Phase 1) — this only checks that ConfigJson parses,
    // never that VariantKey is a "real" registered variant (that validation belongs to the frontend registry).
    private static bool BeValidJson(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(configJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
