using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;

/// <summary>
/// Non-secret Eneba HTTP client tuning, bound from configuration section <c>"Eneba"</c>. Contains
/// nothing sensitive — Auth ID/Auth Secret/account email come from the encrypted
/// <c>Supplier.OAuthSettingsJson</c> field, never from here — mirrors <c>BambooProviderOptions</c>/
/// <c>GlobeTopperProviderOptions</c>'s <see cref="RequestTimeoutSeconds"/>/<see cref="MaxResponseBytes"/> shape.
/// </summary>
public sealed class EnebaProviderOptions
{
    public const string SectionName = "Eneba";

    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>Hard cap on response body size read from a GraphQL call, defends against a misbehaving/compromised endpoint streaming an unbounded body.</summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Hard cap on the downloaded key-export archive, which is not a GraphQL response and can legitimately
    /// be larger (a big wholesale order's key file) — kept as a separate, larger bound rather than reusing
    /// <see cref="MaxResponseBytes"/>.
    /// </summary>
    public int MaxArchiveBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// How many short, in-request polls <see cref="EnebaSupplierProvider.GetOrderStatusAsync"/> makes
    /// against <c>O_orderExport</c> before giving up for this reconciliation tick (never blocks longer
    /// than <see cref="RequestTimeoutSeconds"/> allows) — the sweep's next tick simply tries again, so
    /// this only bounds how long one reconciliation call takes, never how long final delivery can take
    /// overall.
    /// </summary>
    public int ExportPollAttempts { get; set; } = 3;

    public int ExportPollDelaySeconds { get; set; } = 2;
}

/// <summary>Fail-fast validation at startup — mirrors BambooProviderOptionsValidator/GlobeTopperProviderOptionsValidator's identical pattern.</summary>
internal sealed class EnebaProviderOptionsValidator : IValidateOptions<EnebaProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, EnebaProviderOptions options)
    {
        if (options.RequestTimeoutSeconds is <= 0 or > 120)
        {
            return ValidateOptionsResult.Fail("Eneba:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxResponseBytes is <= 0 or > 16 * 1024 * 1024)
        {
            return ValidateOptionsResult.Fail("Eneba:MaxResponseBytes must be between 1 and 16,777,216 (16 MB).");
        }

        if (options.MaxArchiveBytes is <= 0 or > 256 * 1024 * 1024)
        {
            return ValidateOptionsResult.Fail("Eneba:MaxArchiveBytes must be between 1 and 268,435,456 (256 MB).");
        }

        if (options.ExportPollAttempts is <= 0 or > 10)
        {
            return ValidateOptionsResult.Fail("Eneba:ExportPollAttempts must be between 1 and 10.");
        }

        if (options.ExportPollDelaySeconds is <= 0 or > 30)
        {
            return ValidateOptionsResult.Fail("Eneba:ExportPollDelaySeconds must be between 1 and 30.");
        }

        return ValidateOptionsResult.Success;
    }
}
