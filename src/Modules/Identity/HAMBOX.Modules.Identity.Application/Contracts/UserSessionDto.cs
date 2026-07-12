namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Represents an active or historical user session.
/// </summary>
public sealed record UserSessionDto(
    Guid Id,
    string AuthContext,
    string IpAddress,
    string UserAgent,
    string? BrowserName,
    string? DeviceName,
    DateTimeOffset StartedOnUtc,
    DateTimeOffset LastActivityOnUtc,
    DateTimeOffset? EndedOnUtc,
    bool IsActive);
