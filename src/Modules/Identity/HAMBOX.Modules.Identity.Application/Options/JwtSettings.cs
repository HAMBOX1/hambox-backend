namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for configuring JSON Web Token (JWT) options.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Gets the secret key used for signing JWTs.
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the issuer of the tokens.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the audience for the tokens.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets the duration of the access token in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; init; } = 15;

    /// <summary>
    /// Gets the duration of the refresh token in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; init; } = 30;
}
