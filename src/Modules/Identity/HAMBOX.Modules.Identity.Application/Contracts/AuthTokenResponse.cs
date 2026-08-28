namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Represents the authentication tokens returned after successful login or token refresh.
/// </summary>
/// <param name="AccessToken">The JWT access token.</param>
/// <param name="RefreshToken">
/// The plaintext refresh token. This is an internal Application-layer value only — the endpoint
/// layer consumes it to write the HttpOnly refresh cookie and must never serialize this record
/// directly back to the client (see <c>AuthEndpoints.HandleTokenResult</c>).
/// </param>
/// <param name="ExpiresAt">The date and time, in UTC, when the access token expires.</param>
/// <param name="RefreshTokenExpiresAt">
/// The date and time, in UTC, when the refresh token expires — used as the refresh cookie's
/// <c>Expires</c> attribute so the cookie's lifetime matches the persisted token's lifetime exactly.
/// </param>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);
