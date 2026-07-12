using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.SetRolePermissions;

public sealed record SetRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyCollection<Guid> PermissionIds,
    string? IpAddress) : IRequest<Result>;
