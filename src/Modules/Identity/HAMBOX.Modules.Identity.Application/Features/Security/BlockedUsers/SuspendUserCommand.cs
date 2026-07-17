using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

/// <summary>
/// Suspends an active user account. Unlike <see cref="BlockUserCommand"/>, a suspension has no
/// expiration and always requires a manual unblock.
/// </summary>
public sealed record SuspendUserCommand(
    Guid UserId,
    string Reason,
    string? Notes,
    string? IpAddress) : IRequest<Result>;
