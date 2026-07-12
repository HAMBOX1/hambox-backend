using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.AssignUsersToRole;

public sealed record AssignUsersToRoleCommand(
    Guid RoleId,
    IReadOnlyCollection<Guid> UserIds,
    string? IpAddress) : IRequest<Result>;
