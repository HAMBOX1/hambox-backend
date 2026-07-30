using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;

internal sealed class CreateLandingPageTemplateCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateLandingPageTemplateCommand, Result<LandingPageTemplateDetailDto>>
{
    public async Task<Result<LandingPageTemplateDetailDto>> Handle(
        CreateLandingPageTemplateCommand request, CancellationToken cancellationToken)
    {
        string? sectionsJson = null;

        if (request.CloneFromTemplateId is Guid sourceId)
        {
            var source = await dbContext.LandingPageTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == sourceId, cancellationToken);

            if (source is null)
            {
                return Result.Failure<LandingPageTemplateDetailDto>(ContentErrors.TemplateNotFound);
            }

            // Regenerate every InstanceId so the clone's sections never collide in identity with the source's.
            var clonedSections = LandingPageSectionsSerializer
                .Deserialize(source.DraftSectionsJson ?? source.SectionsJson)
                .Select(s => s with { InstanceId = Guid.NewGuid() })
                .ToList();

            sectionsJson = LandingPageSectionsSerializer.Serialize(clonedSections);
        }

        var template = LandingPageTemplate.Create(request.Name, sectionsJson);

        dbContext.LandingPageTemplates.Add(template);
        LandingPageAuditWriter.Record(dbContext, template.Id, LandingPageAuditAction.TemplateCreated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var sections = LandingPageSectionsSerializer.Deserialize(template.SectionsJson);
        return Result.Success(new LandingPageTemplateDetailDto(
            template.Id, template.Name, template.Slug, template.IsActive, template.HasUnpublishedChanges, sections));
    }
}
