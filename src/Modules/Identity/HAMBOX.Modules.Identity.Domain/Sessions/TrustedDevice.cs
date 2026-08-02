using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Sessions;

/// <summary>
/// Tracks a device (identified by <see cref="DeviceFingerprint"/>) recognized across a user's
/// login attempts. Purely investigative/labeling data — <see cref="IsTrusted"/> never bypasses
/// login, permissions, or OTP (see the Security Center design notes). <see cref="IsBlocked"/> is
/// the one status here that *does* enforce: a blocked device is rejected at login, mirroring the
/// existing email/IP/country blocking pattern.
/// </summary>
/// <remarks>
/// Per-login IP/location history for a device is intentionally not duplicated on this entity —
/// it's derived by querying <see cref="LoginHistory"/> for the same <see cref="UserId"/> and
/// <see cref="Fingerprint"/>, reusing the existing audit trail instead of a second store.
/// </remarks>
public sealed class TrustedDevice : Entity
{
    private TrustedDevice()
    {
    }

    private TrustedDevice(
        Guid id,
        Guid userId,
        string fingerprint,
        string? browserName,
        string? osName,
        string? deviceType,
        string ipAddress,
        string? countryCode,
        string? city,
        DateTimeOffset seenOnUtc)
        : base(id)
    {
        UserId = userId;
        Fingerprint = fingerprint;
        BrowserName = browserName;
        OsName = osName;
        DeviceType = deviceType;
        FirstSeenUtc = seenOnUtc;
        LastSeenUtc = seenOnUtc;
        LastIpAddress = ipAddress;
        LastCountryCode = countryCode;
        LastCity = city;
        LoginCount = 1;
    }

    public Guid UserId { get; private set; }

    public string Fingerprint { get; private set; } = string.Empty;

    public string? BrowserName { get; private set; }

    public string? OsName { get; private set; }

    public string? DeviceType { get; private set; }

    public DateTimeOffset FirstSeenUtc { get; private set; }

    public DateTimeOffset LastSeenUtc { get; private set; }

    public string LastIpAddress { get; private set; } = string.Empty;

    public string? LastCountryCode { get; private set; }

    public string? LastCity { get; private set; }

    public int LoginCount { get; private set; }

    public bool IsTrusted { get; private set; }

    public DateTimeOffset? TrustedOnUtc { get; private set; }

    public Guid? TrustedByUserId { get; private set; }

    public bool IsBlocked { get; private set; }

    public DateTimeOffset? BlockedOnUtc { get; private set; }

    public Guid? BlockedByUserId { get; private set; }

    public string? BlockReason { get; private set; }

    public static TrustedDevice Create(
        Guid userId,
        string fingerprint,
        string? browserName,
        string? osName,
        string? deviceType,
        string ipAddress,
        string? countryCode,
        string? city)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        return new TrustedDevice(
            Guid.NewGuid(), userId, fingerprint, browserName, osName, deviceType, ipAddress, countryCode, city, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Records another login from this device, refreshing its "last seen" signals.
    /// </summary>
    public void RecordLogin(string ipAddress, string? countryCode, string? city)
    {
        LastSeenUtc = DateTimeOffset.UtcNow;
        LastIpAddress = ipAddress;
        LastCountryCode = countryCode;
        LastCity = city;
        LoginCount++;
    }

    public void Trust(Guid trustedByUserId)
    {
        IsTrusted = true;
        TrustedOnUtc = DateTimeOffset.UtcNow;
        TrustedByUserId = trustedByUserId;
    }

    public void Untrust()
    {
        IsTrusted = false;
        TrustedOnUtc = null;
        TrustedByUserId = null;
    }

    public void Block(Guid blockedByUserId, string? reason)
    {
        IsBlocked = true;
        BlockedOnUtc = DateTimeOffset.UtcNow;
        BlockedByUserId = blockedByUserId;
        BlockReason = reason;
    }

    public void Unblock()
    {
        IsBlocked = false;
        BlockedOnUtc = null;
        BlockedByUserId = null;
        BlockReason = null;
    }
}
