using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.MaintenanceBypass;

public sealed record VerifyMaintenanceBypassCommand(string Password)
    : IRequest<Result<MaintenanceBypassResponse>>;

public sealed record MaintenanceBypassResponse(string Token, DateTimeOffset ExpiresOnUtc);
