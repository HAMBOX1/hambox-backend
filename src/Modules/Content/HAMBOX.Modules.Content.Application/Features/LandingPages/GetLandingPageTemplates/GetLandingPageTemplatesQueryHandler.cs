using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplates;

internal sealed class GetLandingPageTemplatesQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetLandingPageTemplatesQuery, Result<IReadOnlyList<LandingPageTemplateSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<LandingPageTemplateSummaryDto>>> Handle(
        GetLandingPageTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.LandingPageTemplates.AsNoTracking();

        if (request.Scope is { } scope)
        {
            query = query.Where(t => t.Scope == scope);
        }

        if (request.TargetIds is { Count: > 0 } targetIds)
        {
            query = query.Where(t => t.TargetId != null && targetIds.Contains(t.TargetId!.Value));
        }

        var templates = await query
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.ModifiedOnUtc)
            .ToListAsync(cancellationToken);

        var dtos = templates
            .Select(t => new LandingPageTemplateSummaryDto(
                t.Id,
                t.Name,
                t.Slug,
                t.IsActive,
                t.HasUnpublishedChanges,
                (t.ModifiedOnUtc ?? t.CreatedOnUtc).UtcDateTime,
                t.Scope,
                t.TargetId))
            .ToList();

        return Result.Success<IReadOnlyList<LandingPageTemplateSummaryDto>>(dtos);
    }
}
