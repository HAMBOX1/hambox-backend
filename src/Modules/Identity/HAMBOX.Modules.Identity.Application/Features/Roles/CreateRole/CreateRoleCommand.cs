using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.CreateRole;

public sealed record CreateRoleCommand(
    string Name,
    string? Description,
    int? PriorityLevel,
    string? IpAddress) : IRequest<Result<Guid>>;
