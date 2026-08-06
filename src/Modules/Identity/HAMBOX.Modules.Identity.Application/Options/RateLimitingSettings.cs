namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for per-IP rate limiting on authentication endpoints prone to brute-force/abuse
/// (login, registration, password reset, etc). Complements, not replaces, the existing
/// per-account <see cref="LockoutSettings"/> lockout.
/// </summary>
public sealed class RateLimitingSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Limiter applied to credential-guessing-prone endpoints (login, admin login, Google sign-in).
    /// </summary>
    public RateLimitPolicySettings Login { get; init; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>
    /// Limiter applied to account-action endpoints (register, forgot/reset password, resend verification).
    /// </summary>
    public RateLimitPolicySettings AccountActions { get; init; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

/// <summary>
/// Fixed-window rate limit parameters for a single policy.
/// </summary>
public sealed class RateLimitPolicySettings
{
    /// <summary>
    /// Gets the maximum number of requests permitted per client IP within the window.
    /// </summary>
    public int PermitLimit { get; init; } = 10;

    /// <summary>
    /// Gets the fixed window length, in seconds.
    /// </summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Gets the number of requests allowed to queue once the limit is reached, before being rejected outright.
    /// </summary>
    public int QueueLimit { get; init; }
}
