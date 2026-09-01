using System.Text.Json;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Infrastructure.Currency;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.Extensions.Configuration;

namespace HAMBOX.Modules.Identity.Infrastructure.PlatformSettings;

internal static class PlatformSettingsDefaultsFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    internal static string GetDefaultJson(string categoryKey, IConfiguration configuration)
    {
        var payload = GetDefaultPayload(categoryKey, configuration);
        return JsonSerializer.Serialize(payload, payload?.GetType() ?? typeof(object), JsonOptions);
    }

    internal static object GetDefaultPayload(string categoryKey, IConfiguration configuration) =>
        categoryKey switch
        {
            PlatformSettingsCategoryKeys.General => CreateGeneral(configuration),
            PlatformSettingsCategoryKeys.Branding => CreateBranding(configuration),
            PlatformSettingsCategoryKeys.Storefront => CreateStorefront(),
            PlatformSettingsCategoryKeys.Localization => CreateLocalization(configuration),
            PlatformSettingsCategoryKeys.Currency => CreateCurrency(configuration),
            PlatformSettingsCategoryKeys.Theme => CreateTheme(),
            PlatformSettingsCategoryKeys.Email => CreateEmail(configuration),
            PlatformSettingsCategoryKeys.Authentication => CreateAuthentication(configuration),
            PlatformSettingsCategoryKeys.Security => CreateSecurity(configuration),
            PlatformSettingsCategoryKeys.Otp => CreateOtp(configuration),
            PlatformSettingsCategoryKeys.Totp => CreateTotp(),
            PlatformSettingsCategoryKeys.Commerce => CreateCommerce(),
            PlatformSettingsCategoryKeys.Checkout => CreateCheckout(),
            PlatformSettingsCategoryKeys.Promotions => CreatePromotions(),
            PlatformSettingsCategoryKeys.Memberships => CreateMemberships(),
            PlatformSettingsCategoryKeys.Referral => CreateReferral(),
            PlatformSettingsCategoryKeys.Inventory => CreateInventory(),
            PlatformSettingsCategoryKeys.Queue => CreateQueue(),
            PlatformSettingsCategoryKeys.RetryPolicies => CreateRetryPolicies(),
            PlatformSettingsCategoryKeys.Notifications => CreateNotifications(),
            PlatformSettingsCategoryKeys.Media => CreateMedia(configuration),
            PlatformSettingsCategoryKeys.Seo => CreateSeo(configuration),
            PlatformSettingsCategoryKeys.Analytics => CreateAnalytics(),
            PlatformSettingsCategoryKeys.Integrations => CreateIntegrations(),
            PlatformSettingsCategoryKeys.Maintenance => CreateMaintenance(),
            PlatformSettingsCategoryKeys.Legal => CreateLegal(),
            _ => throw new ArgumentException($"Unknown settings category: {categoryKey}", nameof(categoryKey)),
        };

    internal static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException($"Failed to deserialize settings payload to {typeof(T).Name}.");

    internal static string Serialize<T>(T payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    private static GeneralSettingsPayload CreateGeneral(IConfiguration configuration) =>
        new(
            StoreName: "HAMBOX",
            StoreDescription: "Unlock thousands of titles with instant digital delivery. Experience the safest marketplace for gamers worldwide.",
            ContactEmail: configuration["EmailSettings:FromAddress"] ?? "noreply@hambox.local",
            SupportEmail: configuration["EmailSettings:FromAddress"] ?? "support@hambox.local",
            Phone: "",
            Address: "",
            Timezone: "UTC",
            DefaultLanguage: "en",
            DefaultCurrency: configuration.GetSection(CurrencySettings.SectionName).Get<CurrencySettings>()?.BaseCurrency ?? "USD",
            StoreStatus: "Open");

    private static BrandingSettingsPayload CreateBranding(IConfiguration configuration) =>
        new(
            LogoUrl: null,
            FaviconUrl: null,
            AdminLogoUrl: null,
            PrimaryColor: "#6366f1",
            SecondaryColor: "#0f172a",
            AccentColor: "#22d3ee",
            FooterLogoUrl: null,
            BrowserTitle: "The Next Level of Digital Gaming");

    private static StorefrontContentSettingsPayload CreateStorefront() =>
        new(
            Hero: new StorefrontHeroSectionSettings(
                Title: "The Next Level of Digital",
                Subtitle: "Unlock thousands of titles with instant digital delivery. Experience the safest marketplace for gamers worldwide.",
                PrimaryButtonText: "Shop Now",
                PrimaryButtonUrl: "/products",
                SecondaryButtonText: "View Deals",
                SecondaryButtonUrl: "/products",
                BackgroundImageUrl: "/assets/images/hambox-hero-background.png",
                OverlayImageUrl: "/assets/images/hambox-hero-overlay.png",
                OverlayOpacity: 0.45,
                BadgeText: "NEW SEASON LIVE",
                BadgeColor: "#22d3ee",
                Visible: true),
            PromotionalBanner: new StorefrontPromotionalBannerSettings(
                Title: "Cyberpunk Sale Event",
                Subtitle: "Up to 80% off premium digital titles. Instant global delivery.",
                ImageUrl: "/assets/images/hambox-hero-background.png",
                ButtonText: "Browse Sale",
                ButtonUrl: "/products",
                CountdownSeconds: 15_735,
                CountdownEndsAtUtc: null,
                BackgroundColor: "#111827",
                Visible: true),
            FlashDeals: new StorefrontFlashDealsSettings(
                SectionTitle: "Flash Deals",
                SectionSubtitle: "Limited-time offers ending soon",
                CountdownEnabled: true,
                CountdownDateUtc: null,
                MaximumProducts: 3,
                SortMethod: "PriceDesc",
                CountdownSeconds: 15_735,
                Visible: true),
            FeaturedCollections: new StorefrontFeaturedCollectionsSettings(
                Title: "Trending Now",
                Subtitle: "Hand-picked titles gamers love",
                MaximumItems: 6,
                DisplayStyle: "grid",
                Visible: true),
            PopularCategories: new StorefrontPopularCategoriesSettings(
                SectionTitle: "Popular Categories",
                MaximumCategories: 8,
                SortOrder: "NameAsc",
                Visible: true),
            FeaturedProduct: new StorefrontFeaturedProductSettings(
                ProductId: null,
                Badge: "EDITOR'S CHOICE",
                CallToAction: "View Product",
                Visible: true),
            TrustBar:
            [
                new StorefrontTrustItemSettings(
                    Id: "instant-delivery",
                    IconUrl: "/assets/images/trust/instant-delivery.svg",
                    Title: "Instant Delivery",
                    Description: "Digital codes delivered to your library within seconds of payment.",
                    SortOrder: 1,
                    Visible: true),
                new StorefrontTrustItemSettings(
                    Id: "secure-payment",
                    IconUrl: "/assets/images/trust/secure-payment.svg",
                    Title: "Secure Payment",
                    Description: "Encrypted checkout with trusted payment providers.",
                    SortOrder: 2,
                    Visible: true),
                new StorefrontTrustItemSettings(
                    Id: "support",
                    IconUrl: "/assets/images/trust/support.svg",
                    Title: "24/7 Support",
                    Description: "Our team is ready to help with orders and redemption.",
                    SortOrder: 3,
                    Visible: true),
            ],
            Footer: new StorefrontFooterSettings(
                CompanyName: "HAMBOX",
                Copyright: "© HAMBOX. All rights reserved.",
                SupportEmail: "support@hambox.local",
                SupportPhone: "",
                Address: "",
                WorkingHours: "24/7 digital support",
                FacebookUrl: "https://facebook.com/hamboxstore",
                InstagramUrl: null,
                XUrl: null,
                DiscordUrl: null,
                TelegramUrl: "https://t.me/hambox",
                YouTubeUrl: null,
                TikTokUrl: null,
                WhatsAppUrl: "https://wa.me/201555413000"),
            Seo: new StorefrontSeoDefaultsSettings(
                DefaultMetaTitle: "The Next Level of Digital | HAMBOX",
                DefaultMetaDescription: "Unlock thousands of titles with instant digital delivery.",
                OpenGraphImageUrl: "/assets/images/hambox-hero-background.png",
                TwitterCard: "summary_large_image",
                CanonicalUrl: "http://localhost:4200"),
            NavigationLinks:
            [
                new StorefrontNavLinkSettings(Id: "games", LabelEn: "Games", LabelAr: "الألعاب", Visible: true),
                new StorefrontNavLinkSettings(Id: "gift-cards", LabelEn: "Digital Products", LabelAr: "منتجات رقمية", Visible: true),
                new StorefrontNavLinkSettings(Id: "subscriptions", LabelEn: "Subscriptions", LabelAr: "الاشتراكات", Visible: true),
                new StorefrontNavLinkSettings(Id: "deals", LabelEn: "Deals", LabelAr: "العروض", Visible: true),
            ]);

    private static LocalizationSettingsPayload CreateLocalization(IConfiguration configuration)
    {
        var currency = configuration.GetSection(CurrencySettings.SectionName).Get<CurrencySettings>() ?? new();
        return new LocalizationSettingsPayload(
            DefaultLanguage: "en",
            SupportedLanguages: ["en", "ar"],
            RtlEnabled: true);
    }

    private static CurrencySettingsPayload CreateCurrency(IConfiguration configuration)
    {
        var settings = configuration.GetSection(CurrencySettings.SectionName).Get<CurrencySettings>() ?? new();
        return new CurrencySettingsPayload(
            settings.BaseCurrency,
            settings.SupportedCurrencies,
            settings.RefreshIntervalMinutes,
            settings.Provider,
            settings.ExternalApiUrl,
            settings.StaticRates);
    }

    private static ThemeSettingsPayload CreateTheme() =>
        new("default", true, true);

    private static EmailSettingsPayload CreateEmail(IConfiguration configuration)
    {
        var email = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>() ?? new EmailSettings();
        return new EmailSettingsPayload(
            email.Enabled,
            email.SmtpHost,
            email.SmtpPort,
            email.Username,
            Password: null,
            email.UseSsl,
            email.FromName,
            email.FromAddress,
            email.ApplicationBaseUrl,
            email.VerificationPath,
            email.ResetPasswordPath,
            HasStoredPassword: !string.IsNullOrWhiteSpace(email.Password));
    }

    private static AuthenticationSettingsPayload CreateAuthentication(IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        return new AuthenticationSettingsPayload(
            MinimumPasswordLength: 8,
            RequireNumbers: false,
            RequireSymbols: false,
            RequireUppercase: false,
            SessionTimeoutMinutes: jwt.AccessTokenExpirationMinutes,
            RememberMeDurationDays: jwt.RefreshTokenExpirationDays,
            AdminOtpEnabled: true);
    }

    private static SecuritySettingsPayload CreateSecurity(IConfiguration configuration)
    {
        var lockout = configuration.GetSection(LockoutSettings.SectionName).Get<LockoutSettings>() ?? new LockoutSettings();
        return new SecuritySettingsPayload(
            lockout.MaxFailedAccessAttempts,
            lockout.LockoutDurationMinutes,
            RequireEmailVerification: true,
            SecurityAlertsEnabled: true,
            SecurityAlertMinSeverity: "High",
            SecurityAlertThrottleMinutes: 15);
    }

    private static OtpSettingsPayload CreateOtp(IConfiguration configuration)
    {
        var otp = configuration.GetSection(AdminOtpSettings.SectionName).Get<AdminOtpSettings>() ?? new AdminOtpSettings();
        return new OtpSettingsPayload(
            otp.CodeLength,
            otp.ExpirationMinutes,
            otp.MaxAttempts,
            otp.ResendCooldownSeconds,
            otp.LockoutMinutes);
    }

    private static TotpSettingsPayload CreateTotp() =>
        new(false, true, "HAMBOX Admin", 6, 30);

    // InvoicePrefix default matches the "ORD-" literal every checkout handler hardcoded before this
    // setting was wired in — an install that never touches this field must see no behavior change.
    private static CommerceSettingsPayload CreateCommerce() =>
        new(0m, false, 15, 24, 14, "ORD-", DefaultSupplierMarginPercent: 20m);

    private static CheckoutSettingsPayload CreateCheckout() =>
        new(true, false, "Card", 60);

    private static PromotionsSettingsPayload CreatePromotions() =>
        new(50m, false, 1000, 1);

    private static MembershipsSettingsPayload CreateMemberships() =>
        new(true, 0, true);

    private static ReferralSettingsPayload CreateReferral() =>
        new(false, 100, 0.10m, 30);

    private static InventorySettingsPayload CreateInventory() =>
        new(5, 15, true, "AfterPayment");

    private static QueueSettingsPayload CreateQueue() =>
        new(30, 25, 4, 300);

    private static RetryPoliciesSettingsPayload CreateRetryPolicies() =>
        new(3, 30, 120, 5, UseExponentialBackoff: false);

    private static NotificationsSettingsPayload CreateNotifications() =>
        new(true, false, false, true, true, true);

    private static MediaSettingsPayload CreateMedia(IConfiguration configuration)
    {
        var file = configuration.GetSection(FileStorageSettings.SectionName).Get<FileStorageSettings>() ?? new();
        return new MediaSettingsPayload(
            file.MaxFileSizeBytes,
            file.AllowedContentTypes,
            true,
            128,
            256,
            512);
    }

    private static SeoSettingsPayload CreateSeo(IConfiguration configuration) =>
        new(
            "The Next Level of Digital | HAMBOX",
            "Unlock thousands of titles with instant digital delivery.",
            "/assets/images/hambox-hero-background.png",
            "summary_large_image",
            "index,follow",
            configuration["EmailSettings:ApplicationBaseUrl"] ?? "http://localhost:4200");

    private static AnalyticsSettingsPayload CreateAnalytics() =>
        new(null, null, null, false);

    private static IntegrationsSettingsPayload CreateIntegrations() =>
        new(null, null, null, false);

    private static MaintenanceSettingsPayload CreateMaintenance() =>
        new(
            true,
            "HAMBOX is launching soon. We're putting the finishing touches on the platform — check back shortly!",
            ["Owner", "SuperAdmin", "Administrator"],
            null,
            false);

    private static LegalSettingsPayload CreateLegal() =>
        new(
            "<p>Terms of service content.</p>",
            "<p>Privacy policy content.</p>",
            "<p>Refund policy content.</p>",
            "<p>Support policy content.</p>");
}
