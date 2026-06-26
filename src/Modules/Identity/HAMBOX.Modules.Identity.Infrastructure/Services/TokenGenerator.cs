using System.Security.Cryptography;
using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Cryptographically secure random token generator.
/// </summary>
internal sealed class TokenGenerator : ITokenGenerator
{
    /// <inheritdoc />
    public string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
