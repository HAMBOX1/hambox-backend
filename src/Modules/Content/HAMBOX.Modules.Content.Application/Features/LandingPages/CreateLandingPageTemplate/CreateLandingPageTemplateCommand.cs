using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;

public sealed record CreateLandingPageTemplateCommand(
    string Name,
    Guid? CloneFromTemplateId,
    LandingPageScope Scope = LandingPageScope.Homepage,
    Guid? TargetId = null)
    : IRequest<Result<LandingPageTemplateDetailDto>>;
