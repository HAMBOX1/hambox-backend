using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPageFooter;

public sealed record GetPublishedLandingPageFooterQuery : IRequest<Result<LandingPageFooterDto>>;
