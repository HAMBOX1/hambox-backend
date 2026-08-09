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

        // Homepage must always have at least one template to serve as the live site. Product/Category
        // pages have no such minimum — deleting the last one for a target just means "no marketing page",
        // a normal, expected state that falls back to the regular PDP/category experience.
        if (template.Scope == LandingPageScope.Homepage)
        {
            var homepageCount = await dbContext.LandingPageTemplates
                .CountAsync(t => t.Scope == LandingPageScope.Homepage, cancellationToken);
            if (homepageCount <= 1)
            {
                return Result.Failure(ContentErrors.CannotDeleteLastTemplate);
            }
        }

        template.Delete();
        LandingPageAuditWriter.Record(dbContext, template.Id, LandingPageAuditAction.Deleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
