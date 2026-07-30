using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplates;

public sealed record GetLandingPageTemplatesQuery : IRequest<Result<IReadOnlyList<LandingPageTemplateSummaryDto>>>;
