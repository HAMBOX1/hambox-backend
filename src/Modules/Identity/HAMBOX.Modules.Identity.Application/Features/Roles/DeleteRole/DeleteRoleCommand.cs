using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.DeleteRole;

public sealed record DeleteRoleCommand(Guid RoleId, string? IpAddress) : IRequest<Result>;
