using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.DeleteLandingPageTemplate;

internal sealed class DeleteLandingPageTemplateCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteLandingPageTemplateCommand, Result>
{
    public async Task<Result> Handle(DeleteLandingPageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await dbContext.LandingPageTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure(ContentErrors.TemplateNotFound);
        }

        if (template.IsActive)
        {
            return Result.Failure(ContentErrors.CannotDeleteActiveTemplate);
        }

        var totalCount = await dbContext.LandingPageTemplates.CountAsync(cancellationToken);
        if (totalCount <= 1)
        {
            return Result.Failure(ContentErrors.CannotDeleteLastTemplate);
        }

        template.Delete();
        LandingPageAuditWriter.Record(dbContext, template.Id, LandingPageAuditAction.Deleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
