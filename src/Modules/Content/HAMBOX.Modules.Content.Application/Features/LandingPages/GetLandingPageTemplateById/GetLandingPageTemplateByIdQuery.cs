using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplateById;

public sealed record GetLandingPageTemplateByIdQuery(Guid Id) : IRequest<Result<LandingPageTemplateDetailDto>>;
