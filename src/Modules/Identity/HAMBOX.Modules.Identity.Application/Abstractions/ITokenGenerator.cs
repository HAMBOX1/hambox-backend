namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Generates cryptographically secure tokens for verification and password reset flows.
/// </summary>
public interface ITokenGenerator
{
    /// <summary>
    /// Generates a cryptographically secure, URL-safe token string.
    /// </summary>
    /// <returns>A secure token string.</returns>
    string GenerateSecureToken();
}
