using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Validates JWT settings at application startup.
/// </summary>
internal sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return ValidateOptionsResult.Fail(
                "JwtSettings:SecretKey is required. Set it via environment variable JwtSettings__SecretKey or user secrets.");
        }

        if (options.SecretKey.Length < 32)
        {
            return ValidateOptionsResult.Fail(
                "JwtSettings:SecretKey must be at least 32 characters long.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail("JwtSettings:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail("JwtSettings:Audience is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
