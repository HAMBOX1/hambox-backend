using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplates;

/// <summary>Lists templates, optionally filtered to one <paramref name="Scope"/> and/or a specific set of <paramref name="TargetIds"/> (used for the admin's bulk "does this product/category have a page" lookup).</summary>
public sealed record GetLandingPageTemplatesQuery(LandingPageScope? Scope = null, IReadOnlyList<Guid>? TargetIds = null)
    : IRequest<Result<IReadOnlyList<LandingPageTemplateSummaryDto>>>;
