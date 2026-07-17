namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// The verified identity claims extracted from a Google ID token.
/// </summary>
public sealed record GoogleTokenPayload(
    string Subject,
    string Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName);

/// <summary>
/// Validates Google Sign-In ID tokens.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Verifies the given Google ID token's signature, issuer, audience, and expiry.
    /// </summary>
    /// <returns>The verified payload, or <see langword="null"/> if the token is invalid.</returns>
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
