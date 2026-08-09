using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPage;

internal sealed class GetPublishedLandingPageQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetPublishedLandingPageQuery, Result<PublishedLandingPageDto>>
{
    public async Task<Result<PublishedLandingPageDto>> Handle(
        GetPublishedLandingPageQuery request, CancellationToken cancellationToken)
    {
        var template = await dbContext.LandingPageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.Scope == request.Scope && t.TargetId == request.TargetId, cancellationToken);

        if (template is null)
        {
            return Result.Failure<PublishedLandingPageDto>(ContentErrors.NoActiveTemplate);
        }

        var sections = LandingPageSectionsSerializer.Deserialize(template.SectionsJson)
            .Where(s => s.IsVisible)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var publishedOnUtc = await dbContext.LandingPageAuditLogs
            .AsNoTracking()
            .Where(l => l.TemplateId == template.Id && l.Action == LandingPageAuditAction.Published)
            .OrderByDescending(l => l.CreatedOnUtc)
            .Select(l => (DateTimeOffset?)l.CreatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(new PublishedLandingPageDto(
            template.Id,
            template.Name,
            sections,
            publishedOnUtc?.UtcDateTime,
            template.SeoTitle,
            template.SeoDescription,
            template.SeoOgImageUrl));
    }
}
