using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

/// <summary>
/// Permanently bans a user account. Unlike <see cref="BlockUserCommand"/>, a ban never auto-expires.
/// </summary>
public sealed record BanUserCommand(
    Guid UserId,
    string Reason,
    string? Notes,
    string? IpAddress) : IRequest<Result>;
