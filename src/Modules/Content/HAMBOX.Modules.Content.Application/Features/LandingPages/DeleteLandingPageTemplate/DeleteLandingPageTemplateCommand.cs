using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.DeleteLandingPageTemplate;

public sealed record DeleteLandingPageTemplateCommand(Guid TemplateId) : IRequest<Result>;
