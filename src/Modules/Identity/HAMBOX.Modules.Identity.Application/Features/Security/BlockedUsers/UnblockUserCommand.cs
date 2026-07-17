using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

/// <summary>
/// Restores a suspended, blocked, or banned user account to Active.
/// </summary>
public sealed record UnblockUserCommand(Guid UserId, string? IpAddress) : IRequest<Result>;
