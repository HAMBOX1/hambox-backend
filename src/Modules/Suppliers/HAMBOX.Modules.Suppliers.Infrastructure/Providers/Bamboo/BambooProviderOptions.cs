using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;

/// <summary>
/// Non-secret Bamboo HTTP client tuning, bound from configuration section <c>"Bamboo"</c>. Deliberately
/// contains nothing sensitive — credentials come from the encrypted <c>Supplier</c> entity, never from
/// here, so this type is safe to bind from <c>appsettings.json</c>/environment variables and never needs
/// User Secrets.
/// </summary>
public sealed class BambooProviderOptions
{
    public const string SectionName = "Bamboo";

    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>Hard cap on response body size read from Bamboo, defends against a misbehaving/compromised endpoint streaming an unbounded body.</summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;
}

/// <summary>Fail-fast validation at startup — mirrors JwtSettingsValidator/EmailSettingsValidator's pattern exactly.</summary>
internal sealed class BambooProviderOptionsValidator : IValidateOptions<BambooProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, BambooProviderOptions options)
    {
        if (options.RequestTimeoutSeconds is <= 0 or > 120)
        {
            return ValidateOptionsResult.Fail("Bamboo:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxResponseBytes is <= 0 or > 16 * 1024 * 1024)
        {
            return ValidateOptionsResult.Fail("Bamboo:MaxResponseBytes must be between 1 and 16,777,216 (16 MB).");
        }

        return ValidateOptionsResult.Success;
    }
}
