using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.DiscardLandingPageTemplateDraft;

public sealed record DiscardLandingPageTemplateDraftCommand(Guid TemplateId) : IRequest<Result<LandingPageTemplateDetailDto>>;
