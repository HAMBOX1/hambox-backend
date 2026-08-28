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

    /// <summary>
    /// Limiter applied to the cookie-authenticated <c>refresh</c>/<c>logout</c> endpoints. Deliberately
    /// more generous than <see cref="Login"/>: refresh is not a credential-guessing surface (it requires
    /// an already-issued HttpOnly cookie, not a guessable secret) and legitimate traffic can call it
    /// often — multiple open tabs each independently recovering from a 401, background token renewal,
    /// etc. This exists purely as abuse/DoS backstop, not brute-force defense.
    /// </summary>
    public RateLimitPolicySettings Refresh { get; init; } = new() { PermitLimit = 30, WindowSeconds = 60 };
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
