using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Sessions;

/// <summary>
/// Represents an active user session.
/// </summary>
public sealed class UserSession : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserSession"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private UserSession()
    {
    }

    private UserSession(
        Guid id,
        Guid userId,
        string ipAddress,
        string userAgent,
        string authContext,
        LoginContext? context,
        DateTimeOffset startedOnUtc)
        : base(id)
    {
        UserId = userId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        AuthContext = authContext;
        BrowserName = context?.BrowserName;
        OsName = context?.OsName;
        DeviceName = context?.DeviceType;
        CountryCode = context?.CountryCode;
        City = context?.City;
        Fingerprint = context?.Fingerprint;
        StartedOnUtc = startedOnUtc;
        LastActivityOnUtc = startedOnUtc;
    }

    /// <summary>
    /// Gets the identifier of the user who owns this session.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the IP address from which the session was initiated.
    /// </summary>
    public string IpAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user agent string of the client that initiated the session.
    /// </summary>
    public string UserAgent { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the auth context for this session (customer or admin).
    /// </summary>
    public string AuthContext { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the linked refresh token identifier, when assigned.
    /// </summary>
    public Guid? RefreshTokenId { get; private set; }

    /// <summary>
    /// Gets a parsed browser name from the user agent.
    /// </summary>
    public string? BrowserName { get; private set; }

    /// <summary>
    /// Gets a parsed operating system name from the user agent.
    /// </summary>
    public string? OsName { get; private set; }

    /// <summary>
    /// Gets a parsed device description from the user agent.
    /// </summary>
    public string? DeviceName { get; private set; }

    /// <summary>
    /// Gets the resolved ISO-3166 alpha-2 country code the session originated from, when a
    /// geolocation resolver is configured.
    /// </summary>
    public string? CountryCode { get; private set; }

    /// <summary>
    /// Gets the resolved city the session originated from, when a city-level geolocation
    /// resolver is configured.
    /// </summary>
    public string? City { get; private set; }

    /// <summary>
    /// Gets the device fingerprint (see <see cref="DeviceFingerprint"/>), used to correlate this
    /// session with a <see cref="TrustedDevice"/>.
    /// </summary>
    public string? Fingerprint { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the session was started.
    /// </summary>
    public DateTimeOffset StartedOnUtc { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, of the last recorded activity in this session.
    /// </summary>
    public DateTimeOffset LastActivityOnUtc { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the session ended.
    /// A value of <see langword="null"/> indicates the session is still active.
    /// </summary>
    public DateTimeOffset? EndedOnUtc { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the session is still active.
    /// </summary>
    public bool IsActive => EndedOnUtc is null;

    /// <summary>
    /// Creates a new user session.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="ipAddress">The client IP address.</param>
    /// <param name="userAgent">The client user agent string.</param>
    /// <param name="authContext">The auth context (customer or admin).</param>
    /// <param name="context">The resolved geolocation/device enrichment signals, if any.</param>
    /// <returns>A new <see cref="UserSession"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when any required parameter is invalid.</exception>
    public static UserSession Create(
        Guid userId,
        string ipAddress,
        string userAgent,
        string authContext,
        LoginContext? context = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(authContext);

        return new UserSession(
            Guid.NewGuid(),
            userId,
            ipAddress,
            userAgent,
            authContext,
            context,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Links the active refresh token to this session.
    /// </summary>
    public void LinkRefreshToken(Guid refreshTokenId)
    {
        if (refreshTokenId == Guid.Empty)
        {
            throw new ArgumentException("Refresh token identifier is required.", nameof(refreshTokenId));
        }

        RefreshTokenId = refreshTokenId;
        LastActivityOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records user activity, updating the last activity timestamp.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the session has already ended.</exception>
    public void RecordActivity()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot record activity on an ended session.");
        }

        LastActivityOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Ends the session.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the session has already ended.</exception>
    public void End()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Session has already ended.");
        }

        EndedOnUtc = DateTimeOffset.UtcNow;
    }
}
