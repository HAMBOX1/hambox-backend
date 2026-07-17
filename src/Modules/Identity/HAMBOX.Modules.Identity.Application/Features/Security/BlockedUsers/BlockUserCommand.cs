using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

/// <summary>
/// Blocks a user account, temporarily if <paramref name="ExpiresOnUtc"/> is supplied.
/// </summary>
public sealed record BlockUserCommand(
    Guid UserId,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    string? IpAddress) : IRequest<Result>;
