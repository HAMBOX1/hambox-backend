namespace HAMBOX.Application.Security;

/// <summary>
/// Cloudflare Turnstile configuration. Bound from configuration section <c>"Turnstile"</c>
/// (<see cref="SectionName"/>). <see cref="SecretKey"/> must only ever come from an environment
/// variable/user-secret/VPS secret file — never from a file tracked in source control. <see cref="SiteKey"/>
/// is not sensitive (Cloudflare's own widget embeds it in page HTML) and is safe to expose to the frontend.
/// </summary>
public sealed class TurnstileSettings
{
    public const string SectionName = "Turnstile";

    /// <summary>Public widget key. Safe to expose to the frontend.</summary>
    public string SiteKey { get; init; } = string.Empty;

    /// <summary>Server-side verification secret. Must never leave the backend.</summary>
    public string SecretKey { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Optional defense-in-depth check: when set, a Siteverify response reporting a different
    /// <c>hostname</c> than this value is treated as a failed verification. Empty (default) skips the
    /// check — Cloudflare already restricts which domains a given SiteKey's widget can be embedded on
    /// via the Turnstile dashboard, so this is an additional server-side check, not the primary control.
    /// </summary>
    public string ExpectedHostname { get; init; } = string.Empty;
}

/// <summary>
/// Server-side verification of a Cloudflare Turnstile token against the Siteverify API. Implemented in
/// BuildingBlocks Infrastructure and registered in DI so any module can require it on a high-risk
/// command's validator without taking a dependency on a specific module. Every implementation must fail
/// closed: a token that cannot be verified (network error, timeout, malformed response, missing token)
/// is treated as a failed verification, never as "skip the check."
/// </summary>
public interface ITurnstileVerificationService
{
    /// <summary>
    /// Verifies <paramref name="token"/> with Cloudflare. Returns <see langword="false"/> for a missing/
    /// empty token, a token Cloudflare rejects, or any failure to complete verification (fail closed).
    /// Never throws for an invalid/expired/malformed token — only for genuinely unexpected conditions the
    /// caller cannot reasonably handle.
    /// </summary>
    /// <param name="token">The client-supplied Turnstile response token.</param>
    /// <param name="remoteIp">The caller's IP address, forwarded to Cloudflare for its own risk scoring. Optional.</param>
    /// <param name="expectedAction">
    /// The widget <c>action</c> name expected for the operation being protected (e.g. <c>"register"</c>).
    /// When supplied, a Siteverify response reporting a different action is treated as a failed
    /// verification — this stops a token minted for one protected flow being replayed on another. Pass
    /// <see langword="null"/> to skip the check.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> VerifyAsync(string? token, string? remoteIp, string? expectedAction, CancellationToken cancellationToken);
}
