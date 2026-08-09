using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplateById;

internal sealed class GetLandingPageTemplateByIdQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetLandingPageTemplateByIdQuery, Result<LandingPageTemplateDetailDto>>
{
    public async Task<Result<LandingPageTemplateDetailDto>> Handle(
        GetLandingPageTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await dbContext.LandingPageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
        {
            return Result.Failure<LandingPageTemplateDetailDto>(ContentErrors.TemplateNotFound);
        }

        // Editing state: an unpublished draft (if any) takes precedence over the published sections/SEO.
        var sections = LandingPageSectionsSerializer.Deserialize(template.DraftSectionsJson ?? template.SectionsJson);
        var hasDraft = template.DraftSectionsJson is not null;

        return Result.Success(new LandingPageTemplateDetailDto(
            template.Id,
            template.Name,
            template.Slug,
            template.IsActive,
            template.HasUnpublishedChanges,
            sections,
            template.Scope,
            template.TargetId,
            hasDraft ? template.DraftSeoTitle : template.SeoTitle,
            hasDraft ? template.DraftSeoDescription : template.SeoDescription,
            hasDraft ? template.DraftSeoOgImageUrl : template.SeoOgImageUrl));
    }
}
