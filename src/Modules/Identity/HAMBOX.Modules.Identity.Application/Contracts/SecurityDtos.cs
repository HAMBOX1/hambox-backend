namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Summary information for a restricted (suspended/blocked/banned) user in list views.
/// </summary>
public sealed record BlockedUserListItemDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    string? BlockReason,
    string? BlockNotes,
    DateTimeOffset? BlockExpiresOnUtc,
    DateTimeOffset? BlockedOnUtc);

/// <summary>
/// A blocked email address or wildcard domain pattern.
/// </summary>
public sealed record BlockedEmailDto(
    Guid Id,
    string Pattern,
    bool IsWildcardDomain,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    DateTimeOffset CreatedOnUtc);

/// <summary>
/// The access status of a single ISO-3166 country, merging the enumerated country list with any
/// administrator override.
/// </summary>
public sealed record CountryRestrictionDto(
    string CountryCode,
    string CountryName,
    string Status,
    string? Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc);

/// <summary>
/// A blocked IP address or CIDR range.
/// </summary>
public sealed record BlockedIpDto(
    Guid Id,
    string CidrOrAddress,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    DateTimeOffset CreatedOnUtc);

/// <summary>
/// A single security event log entry for list/detail display.
/// </summary>
public sealed record SecurityEventDto(
    Guid Id,
    string EventType,
    string Severity,
    string Description,
    Guid? ActorUserId,
    string? ActorEmail,
    Guid? TargetUserId,
    string? TargetEmail,
    string? IpAddress,
    string? Country,
    string? UserAgent,
    string? CorrelationId,
    DateTimeOffset OccurredOnUtc);

/// <summary>
/// Aggregate counts and recent activity for the Security Center overview dashboard.
/// </summary>
public sealed record SecurityDashboardDto(
    int BlockedUsers,
    int SuspendedUsers,
    int BlockedEmails,
    int BlockedDomains,
    int BlockedCountries,
    int BlockedIps,
    int SecurityEventsToday,
    int FailedLoginsToday,
    IReadOnlyCollection<SecurityEventDto> RecentEvents);

public sealed record BlockUserRequest(string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record SuspendUserRequest(string Reason, string? Notes);

public sealed record BanUserRequest(string Reason, string? Notes);

public sealed record CreateBlockedEmailRequest(string Pattern, string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record CreateBlockedIpRequest(string CidrOrAddress, string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record SetCountryRestrictionRequest(string Status, string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);
