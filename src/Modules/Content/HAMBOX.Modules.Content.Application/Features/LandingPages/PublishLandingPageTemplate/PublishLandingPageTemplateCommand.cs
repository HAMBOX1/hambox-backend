using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.PublishLandingPageTemplate;

public sealed record PublishLandingPageTemplateCommand(Guid TemplateId) : IRequest<Result<LandingPageTemplateDetailDto>>;
