namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// A single login attempt record for the admin-facing Login Events / investigation views.
/// </summary>
public sealed record LoginHistoryDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string IpAddress,
    string? CountryCode,
    string? City,
    string? BrowserName,
    string? OsName,
    string? DeviceType,
    bool IsSuccessful,
    string? FailureReason,
    string? RiskLevel,
    DateTimeOffset OccurredOnUtc);
