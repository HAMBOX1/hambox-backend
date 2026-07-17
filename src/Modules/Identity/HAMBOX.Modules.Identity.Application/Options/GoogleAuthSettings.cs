namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for validating Google Sign-In ID tokens.
/// </summary>
public sealed class GoogleAuthSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "GoogleAuthSettings";

    /// <summary>
    /// Gets the OAuth 2.0 client ID that Google ID tokens must be issued for.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;
}
