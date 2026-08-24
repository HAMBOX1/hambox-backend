namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Represents the authentication tokens returned after successful login or token refresh.
/// </summary>
/// <param name="AccessToken">The JWT access token.</param>
/// <param name="RefreshToken">
/// The plaintext refresh token. Internal contract only — the Presentation layer (see
/// <c>AuthEndpoints.cs</c>) transports this as an HttpOnly cookie and must never serialize this
/// property into a JSON response body.
/// </param>
/// <param name="ExpiresAt">The date and time, in UTC, when the access token expires.</param>
/// <param name="RefreshExpiresAt">
/// The date and time, in UTC, when the refresh token itself expires — used to set the refresh
/// cookie's expiry so it matches the token's real lifetime (including the remember-me case).
/// </param>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RefreshExpiresAt);
