using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.PublishLandingPageTemplate;

internal sealed class PublishLandingPageTemplateCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<PublishLandingPageTemplateCommand, Result<LandingPageTemplateDetailDto>>
{
    public async Task<Result<LandingPageTemplateDetailDto>> Handle(
        PublishLandingPageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await dbContext.LandingPageTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure<LandingPageTemplateDetailDto>(ContentErrors.TemplateNotFound);
        }

        if (!template.HasUnpublishedChanges)
        {
            return Result.Failure<LandingPageTemplateDetailDto>(ContentErrors.NoDraftToPublish);
        }

        template.PublishDraft();
        LandingPageAuditWriter.Record(dbContext, template.Id, LandingPageAuditAction.Published, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var sections = LandingPageSectionsSerializer.Deserialize(template.SectionsJson);
        return Result.Success(new LandingPageTemplateDetailDto(
            template.Id,
            template.Name,
            template.Slug,
            template.IsActive,
            template.HasUnpublishedChanges,
            sections,
            template.Scope,
            template.TargetId,
            template.SeoTitle,
            template.SeoDescription,
            template.SeoOgImageUrl));
    }
}
