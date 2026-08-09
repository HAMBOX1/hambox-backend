using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPage;

/// <summary>Resolves the single published+active template for a scope/target. Defaults to the Homepage (no target) — the existing call sites are unaffected.</summary>
public sealed record GetPublishedLandingPageQuery(LandingPageScope Scope = LandingPageScope.Homepage, Guid? TargetId = null)
    : IRequest<Result<PublishedLandingPageDto>>;
