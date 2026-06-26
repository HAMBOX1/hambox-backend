namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Represents the authentication tokens returned after successful login or token refresh.
/// </summary>
/// <param name="AccessToken">The JWT access token.</param>
/// <param name="RefreshToken">The refresh token for obtaining new access tokens.</param>
/// <param name="ExpiresAt">The date and time, in UTC, when the access token expires.</param>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
