using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPageFooter;

internal sealed class GetPublishedLandingPageFooterQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetPublishedLandingPageFooterQuery, Result<LandingPageFooterDto>>
{
    public async Task<Result<LandingPageFooterDto>> Handle(
        GetPublishedLandingPageFooterQuery request, CancellationToken cancellationToken)
    {
        var template = await dbContext.LandingPageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

        if (template is null)
        {
            return Result.Failure<LandingPageFooterDto>(ContentErrors.NoActiveTemplate);
        }

        var footer = LandingPageSectionsSerializer.Deserialize(template.SectionsJson)
            .FirstOrDefault(s => string.Equals(s.Category, "Footer", StringComparison.OrdinalIgnoreCase));

        return Result.Success(new LandingPageFooterDto(footer));
    }
}
