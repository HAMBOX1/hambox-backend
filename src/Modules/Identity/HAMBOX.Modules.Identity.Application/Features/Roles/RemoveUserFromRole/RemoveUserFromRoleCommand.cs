using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.RemoveUserFromRole;

public sealed record RemoveUserFromRoleCommand(
    Guid RoleId,
    Guid UserId,
    string? IpAddress) : IRequest<Result>;
