using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Domain.Sessions;

/// <summary>
/// Represents an immutable record of a login attempt.
/// </summary>
public sealed class LoginHistory : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoginHistory"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private LoginHistory()
    {
    }

    private LoginHistory(
        Guid id,
        Guid userId,
        string ipAddress,
        string userAgent,
        bool isSuccessful,
        string? failureReason,
        LoginContext? context,
        SecurityEventSeverity? riskLevel)
        : base(id)
    {
        UserId = userId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        IsSuccessful = isSuccessful;
        FailureReason = failureReason;
        CountryCode = context?.CountryCode;
        City = context?.City;
        BrowserName = context?.BrowserName;
        OsName = context?.OsName;
        DeviceType = context?.DeviceType;
        Fingerprint = context?.Fingerprint;
        RiskLevel = riskLevel;
    }

    /// <summary>
    /// Gets the identifier of the user associated with this login attempt.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the IP address from which the login was attempted.
    /// </summary>
    public string IpAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user agent string of the client that attempted the login.
    /// </summary>
    public string UserAgent { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the login attempt was successful.
    /// </summary>
    public bool IsSuccessful { get; private set; }

    /// <summary>
    /// Gets the reason for login failure.
    /// A value of <see langword="null"/> indicates the login was successful.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Gets the resolved ISO-3166 alpha-2 country code, when a geolocation resolver is configured.
    /// </summary>
    public string? CountryCode { get; private set; }

    /// <summary>
    /// Gets the resolved city, when a city-level geolocation resolver is configured.
    /// </summary>
    public string? City { get; private set; }

    /// <summary>
    /// Gets the parsed browser name (e.g. "Chrome 120").
    /// </summary>
    public string? BrowserName { get; private set; }

    /// <summary>
    /// Gets the parsed operating system name (e.g. "Windows 10").
    /// </summary>
    public string? OsName { get; private set; }

    /// <summary>
    /// Gets the coarse device type bucket: "Desktop", "Mobile", or "Tablet".
    /// </summary>
    public string? DeviceType { get; private set; }

    /// <summary>
    /// Gets the device fingerprint (see <see cref="DeviceFingerprint"/>), used to correlate this
    /// attempt with a <see cref="TrustedDevice"/>.
    /// </summary>
    public string? Fingerprint { get; private set; }

    /// <summary>
    /// Gets the computed risk level for this attempt. Reuses <see cref="SecurityEventSeverity"/>
    /// rather than a parallel enum, since the two scales mean the same thing.
    /// </summary>
    public SecurityEventSeverity? RiskLevel { get; private set; }

    /// <summary>
    /// Records a successful login attempt.
    /// </summary>
    public static LoginHistory RecordSuccess(
        Guid userId,
        string ipAddress,
        string userAgent,
        LoginContext? context = null,
        SecurityEventSeverity? riskLevel = null)
    {
        ValidateCommonFields(userId, ipAddress, userAgent);

        return new LoginHistory(Guid.NewGuid(), userId, ipAddress, userAgent, true, null, context, riskLevel);
    }

    /// <summary>
    /// Records a failed login attempt.
    /// </summary>
    public static LoginHistory RecordFailure(
        Guid userId,
        string ipAddress,
        string userAgent,
        string failureReason,
        LoginContext? context = null,
        SecurityEventSeverity? riskLevel = null)
    {
        ValidateCommonFields(userId, ipAddress, userAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        return new LoginHistory(Guid.NewGuid(), userId, ipAddress, userAgent, false, failureReason, context, riskLevel);
    }

    private static void ValidateCommonFields(Guid userId, string ipAddress, string userAgent)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
    }
}
