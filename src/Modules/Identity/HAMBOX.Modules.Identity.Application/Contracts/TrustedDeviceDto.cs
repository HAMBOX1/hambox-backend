namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// A device recognized across a user's login attempts, for the admin Trusted Devices view.
/// <see cref="DisplayName"/> is computed at query time from the parsed browser/OS rather than
/// stored, so it never drifts from the underlying signals.
/// </summary>
public sealed record TrustedDeviceDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string DisplayName,
    string? BrowserName,
    string? OsName,
    string? DeviceType,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    string LastIpAddress,
    string? LastCountryCode,
    string? LastCity,
    int LoginCount,
    bool IsTrusted,
    DateTimeOffset? TrustedOnUtc,
    bool IsBlocked,
    DateTimeOffset? BlockedOnUtc,
    string? BlockReason);
