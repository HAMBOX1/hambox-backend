using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.DuplicateRole;

public sealed record DuplicateRoleCommand(
    Guid RoleId,
    string? NewName,
    string? IpAddress) : IRequest<Result<Guid>>;
