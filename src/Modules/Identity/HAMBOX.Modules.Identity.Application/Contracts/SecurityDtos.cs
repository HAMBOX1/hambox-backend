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
    string? City,
    string? UserAgent,
    string? CorrelationId,
    DateTimeOffset OccurredOnUtc,
    string Status,
    Guid? AcknowledgedByUserId,
    DateTimeOffset? AcknowledgedOnUtc,
    Guid? ResolvedByUserId,
    DateTimeOffset? ResolvedOnUtc,
    string? ResolutionNotes);

/// <summary>
/// A single point in the "logins over time" chart series.
/// </summary>
public sealed record LoginTrendPointDto(DateOnly Date, int SuccessfulLogins, int FailedLogins);

/// <summary>
/// A country ranked by its failed-login count, for the "top countries" chart.
/// </summary>
public sealed record CountryFailureCountDto(string CountryCode, int FailedLogins);

/// <summary>
/// Meaningful, decision-oriented metrics for the Security Center overview — deliberately not a
/// flat list of table-row counts (see the old <c>BlockedUsers</c>/<c>BlockedEmails</c>/... shape
/// this replaced): every field here is something an owner would act on.
/// </summary>
public sealed record SecurityDashboardDto(
    int OpenAlerts,
    int FailedLoginsLast24h,
    int FailedLoginsPrevious24h,
    int ActiveSessions,
    int NewDevicesLast7Days,
    IReadOnlyCollection<LoginTrendPointDto> LoginTrend,
    IReadOnlyCollection<CountryFailureCountDto> TopFailureCountries,
    IReadOnlyCollection<SecurityEventDto> OpenAlertsPreview);

public sealed record BlockUserRequest(string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record SuspendUserRequest(string Reason, string? Notes);

public sealed record BanUserRequest(string Reason, string? Notes);

public sealed record CreateBlockedEmailRequest(string Pattern, string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record CreateBlockedIpRequest(string CidrOrAddress, string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record SetCountryRestrictionRequest(string Status, string Reason, string? Notes, DateTimeOffset? ExpiresOnUtc);

public sealed record UpdateSecurityEventStatusRequest(string Status, string? Notes);

public sealed record BlockDeviceRequest(string? Reason);
