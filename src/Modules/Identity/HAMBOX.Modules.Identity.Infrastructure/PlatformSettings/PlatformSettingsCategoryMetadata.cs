namespace HAMBOX.Modules.Identity.Infrastructure.PlatformSettings;

internal static class PlatformSettingsCategoryMetadata
{
    internal sealed record Meta(string Key, string Label, string Group);

    internal static readonly IReadOnlyList<Meta> All =
    [
        new("general", "General", "Platform"),
        new("branding", "Branding", "Platform"),
        new("storefront", "Storefront", "Platform"),
        new("localization", "Localization", "Platform"),
        new("currency", "Currency", "Platform"),
        new("theme", "Theme", "Platform"),
        new("email", "Email (SMTP)", "Communications"),
        new("authentication", "Authentication", "Security"),
        new("security", "Security", "Security"),
        new("otp", "OTP", "Security"),
        new("totp", "TOTP", "Security"),
        new("commerce", "Commerce", "Commerce"),
        new("checkout", "Checkout", "Commerce"),
        new("promotions", "Promotions", "Commerce"),
        new("memberships", "Memberships", "Commerce"),
        new("referral", "Referral", "Commerce"),
        new("inventory", "Inventory", "Operations"),
        new("queue", "Queue", "Operations"),
        new("retryPolicies", "Retry Policies", "Operations"),
        new("notifications", "Notifications", "Communications"),
        new("media", "Media", "Content"),
        new("seo", "SEO", "Content"),
        new("analytics", "Analytics", "Content"),
        new("integrations", "Integrations", "Platform"),
        new("maintenance", "Maintenance Mode", "Platform"),
        new("legal", "Legal", "Platform"),
    ];

    internal static Meta GetRequired(string key) =>
        All.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown settings category: {key}", nameof(key));
}
