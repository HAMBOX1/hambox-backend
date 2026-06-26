namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for account lockout after failed sign-in attempts.
/// </summary>
public sealed class LockoutSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "LockoutSettings";

    /// <summary>
    /// Gets the maximum number of consecutive failed access attempts before lockout.
    /// </summary>
    public int MaxFailedAccessAttempts { get; init; } = 5;

    /// <summary>
    /// Gets the lockout duration in minutes.
    /// </summary>
    public int LockoutDurationMinutes { get; init; } = 15;
}
