using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.SaveLandingPageTemplateDraft;

public sealed record SaveLandingPageTemplateDraftCommand(
    Guid TemplateId,
    IReadOnlyList<LandingPageSectionEntry> Sections,
    string? SeoTitle = null,
    string? SeoDescription = null,
    string? SeoOgImageUrl = null)
    : IRequest<Result<LandingPageTemplateDetailDto>>;
