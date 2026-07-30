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
        var templates = await dbContext.LandingPageTemplates
            .AsNoTracking()
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
                (t.ModifiedOnUtc ?? t.CreatedOnUtc).UtcDateTime))
            .ToList();

        return Result.Success<IReadOnlyList<LandingPageTemplateSummaryDto>>(dtos);
    }
}
