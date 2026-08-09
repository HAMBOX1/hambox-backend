using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedProductMarketingPageAvailability;

internal sealed class GetPublishedProductMarketingPageAvailabilityQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetPublishedProductMarketingPageAvailabilityQuery, Result<IReadOnlyList<Guid>>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        GetPublishedProductMarketingPageAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (request.ProductIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<Guid>>(Array.Empty<Guid>());
        }

        var available = await dbContext.LandingPageTemplates
            .AsNoTracking()
            .Where(t => t.Scope == LandingPageScope.Product && t.IsActive && t.TargetId != null
                && request.ProductIds.Contains(t.TargetId!.Value))
            .Select(t => t.TargetId!.Value)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<Guid>>(available);
    }
}
