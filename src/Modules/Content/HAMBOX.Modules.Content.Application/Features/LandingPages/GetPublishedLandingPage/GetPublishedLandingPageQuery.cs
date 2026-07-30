using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPage;

public sealed record GetPublishedLandingPageQuery : IRequest<Result<PublishedLandingPageDto>>;
