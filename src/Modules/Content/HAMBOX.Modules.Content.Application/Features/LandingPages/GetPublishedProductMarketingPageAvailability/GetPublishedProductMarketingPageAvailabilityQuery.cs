using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedProductMarketingPageAvailability;

/// <summary>
/// Storefront-facing batched check: of the given product ids, which have a published (Active,
/// Product-scoped) marketing page? Used to decide whether to show a "discover" affordance on a
/// product card without fetching every product's full landing page or making one request per card.
/// </summary>
public sealed record GetPublishedProductMarketingPageAvailabilityQuery(IReadOnlyList<Guid> ProductIds)
    : IRequest<Result<IReadOnlyList<Guid>>>;
