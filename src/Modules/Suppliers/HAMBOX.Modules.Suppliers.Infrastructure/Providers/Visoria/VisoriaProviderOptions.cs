using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;

/// <summary>
/// Non-secret Visoria HTTP client tuning, bound from configuration section <c>"Visoria"</c>. Contains
/// nothing sensitive — credentials come from the encrypted <c>Supplier</c> entity, never from here —
/// mirrors <c>BambooProviderOptions</c> exactly.
/// </summary>
public sealed class VisoriaProviderOptions
{
    public const string SectionName = "Visoria";

    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>Hard cap on response body size read from Visoria, defends against a misbehaving/compromised endpoint streaming an unbounded body.</summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;
}

/// <summary>Fail-fast validation at startup — mirrors BambooProviderOptionsValidator's identical pattern.</summary>
internal sealed class VisoriaProviderOptionsValidator : IValidateOptions<VisoriaProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, VisoriaProviderOptions options)
    {
        if (options.RequestTimeoutSeconds is <= 0 or > 120)
        {
            return ValidateOptionsResult.Fail("Visoria:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxResponseBytes is <= 0 or > 16 * 1024 * 1024)
        {
            return ValidateOptionsResult.Fail("Visoria:MaxResponseBytes must be between 1 and 16,777,216 (16 MB).");
        }

        return ValidateOptionsResult.Success;
    }
}
