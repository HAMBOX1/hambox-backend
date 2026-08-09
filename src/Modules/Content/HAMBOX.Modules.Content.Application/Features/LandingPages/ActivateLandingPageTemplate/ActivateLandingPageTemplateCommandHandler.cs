using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.ActivateLandingPageTemplate;

internal sealed class ActivateLandingPageTemplateCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ActivateLandingPageTemplateCommand, Result<LandingPageTemplateSummaryDto>>
{
    public async Task<Result<LandingPageTemplateSummaryDto>> Handle(
        ActivateLandingPageTemplateCommand request, CancellationToken cancellationToken)
    {
        var target = await dbContext.LandingPageTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (target is null)
        {
            return Result.Failure<LandingPageTemplateSummaryDto>(ContentErrors.TemplateNotFound);
        }

        if (!target.IsActive)
        {
            // Scoped to the same (Scope, TargetId) family only — activating a page for Product A
            // must never deactivate a page for Product B or the homepage.
            var currentlyActive = await dbContext.LandingPageTemplates
                .Where(t => t.IsActive && t.Id != target.Id && t.Scope == target.Scope && t.TargetId == target.TargetId)
                .ToListAsync(cancellationToken);

            foreach (var other in currentlyActive)
            {
                other.Deactivate();
            }

            target.Activate();
            LandingPageAuditWriter.Record(dbContext, target.Id, LandingPageAuditAction.Activated, currentUser.UserId);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new LandingPageTemplateSummaryDto(
            target.Id,
            target.Name,
            target.Slug,
            target.IsActive,
            target.HasUnpublishedChanges,
            (target.ModifiedOnUtc ?? target.CreatedOnUtc).UtcDateTime,
            target.Scope,
            target.TargetId));
    }
}
