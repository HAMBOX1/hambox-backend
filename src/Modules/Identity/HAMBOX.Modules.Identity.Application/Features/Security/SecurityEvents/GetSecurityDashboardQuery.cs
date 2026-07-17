using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

public sealed record GetSecurityDashboardQuery : IRequest<Result<SecurityDashboardDto>>;
