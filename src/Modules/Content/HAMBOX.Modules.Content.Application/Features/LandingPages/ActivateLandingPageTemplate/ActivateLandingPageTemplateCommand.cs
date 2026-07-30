using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.ActivateLandingPageTemplate;

public sealed record ActivateLandingPageTemplateCommand(Guid TemplateId) : IRequest<Result<LandingPageTemplateSummaryDto>>;
