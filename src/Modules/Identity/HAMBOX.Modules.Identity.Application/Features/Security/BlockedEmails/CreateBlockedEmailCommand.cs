using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

/// <summary>
/// Blocks a single email address (<c>user@example.com</c>) or an entire domain via wildcard
/// (<c>*@spamdomain.com</c>). Blocks registration, login, and password reset for matching addresses.
/// </summary>
public sealed record CreateBlockedEmailCommand(
    string Pattern,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    string? IpAddress) : IRequest<Result<Guid>>;
