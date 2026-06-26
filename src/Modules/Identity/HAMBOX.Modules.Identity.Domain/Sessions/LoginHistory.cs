using HAMBOX.Domain.Entities;

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
        string? failureReason)
        : base(id)
    {
        UserId = userId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        IsSuccessful = isSuccessful;
        FailureReason = failureReason;
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
    /// Records a successful login attempt.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="ipAddress">The client IP address.</param>
    /// <param name="userAgent">The client user agent string.</param>
    /// <returns>A new <see cref="LoginHistory"/> instance representing a successful login.</returns>
    /// <exception cref="ArgumentException">Thrown when any required parameter is invalid.</exception>
    public static LoginHistory RecordSuccess(Guid userId, string ipAddress, string userAgent)
    {
        ValidateCommonFields(userId, ipAddress, userAgent);

        return new LoginHistory(Guid.NewGuid(), userId, ipAddress, userAgent, true, null);
    }

    /// <summary>
    /// Records a failed login attempt.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="ipAddress">The client IP address.</param>
    /// <param name="userAgent">The client user agent string.</param>
    /// <param name="failureReason">The reason for the login failure.</param>
    /// <returns>A new <see cref="LoginHistory"/> instance representing a failed login.</returns>
    /// <exception cref="ArgumentException">Thrown when any required parameter is invalid.</exception>
    public static LoginHistory RecordFailure(
        Guid userId,
        string ipAddress,
        string userAgent,
        string failureReason)
    {
        ValidateCommonFields(userId, ipAddress, userAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        return new LoginHistory(Guid.NewGuid(), userId, ipAddress, userAgent, false, failureReason);
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
