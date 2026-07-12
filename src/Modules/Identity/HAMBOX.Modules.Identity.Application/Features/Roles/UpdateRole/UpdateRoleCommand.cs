using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.UpdateRole;

public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string? Description,
    int? PriorityLevel,
    bool? IsDefault,
    string? IpAddress) : IRequest<Result>;
