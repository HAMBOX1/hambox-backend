using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.SaveLandingPageTemplateDraft;

internal sealed class SaveLandingPageTemplateDraftCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveLandingPageTemplateDraftCommand, Result<LandingPageTemplateDetailDto>>
{
    public async Task<Result<LandingPageTemplateDetailDto>> Handle(
        SaveLandingPageTemplateDraftCommand request, CancellationToken cancellationToken)
    {
        var template = await dbContext.LandingPageTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure<LandingPageTemplateDetailDto>(ContentErrors.TemplateNotFound);
        }

        template.SaveDraft(LandingPageSectionsSerializer.Serialize(request.Sections));
        LandingPageAuditWriter.Record(dbContext, template.Id, LandingPageAuditAction.DraftSaved, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new LandingPageTemplateDetailDto(
            template.Id, template.Name, template.Slug, template.IsActive, template.HasUnpublishedChanges, request.Sections));
    }
}
