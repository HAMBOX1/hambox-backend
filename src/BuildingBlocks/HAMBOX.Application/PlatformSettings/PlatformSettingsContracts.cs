namespace HAMBOX.Application.PlatformSettings;

public static class PlatformSettingsCategoryKeys
{
    public const string General = "general";
    public const string Branding = "branding";
    public const string Storefront = "storefront";
    public const string Localization = "localization";
    public const string Currency = "currency";
    public const string Theme = "theme";
    public const string Email = "email";
    public const string Authentication = "authentication";
    public const string Security = "security";
    public const string Otp = "otp";
    public const string Totp = "totp";
    public const string Commerce = "commerce";
    public const string Checkout = "checkout";
    public const string Promotions = "promotions";
    public const string Memberships = "memberships";
    public const string Referral = "referral";
    public const string Inventory = "inventory";
    public const string Queue = "queue";
    public const string RetryPolicies = "retryPolicies";
    public const string Notifications = "notifications";
    public const string Media = "media";
    public const string Seo = "seo";
    public const string Analytics = "analytics";
    public const string Integrations = "integrations";
    public const string Maintenance = "maintenance";
    public const string Legal = "legal";

    public static readonly IReadOnlyList<string> All =
    [
        General, Branding, Storefront, Localization, Currency, Theme, Email,
        Authentication, Security, Otp, Totp, Commerce, Checkout, Promotions,
        Memberships, Referral, Inventory, Queue, RetryPolicies, Notifications,
        Media, Seo, Analytics, Integrations, Maintenance, Legal,
    ];
}

public sealed record PlatformSettingsCategoryDto(
    string Key,
    string Label,
    string Group,
    object Payload,
    DateTimeOffset? ModifiedOnUtc,
    bool HasPersistedOverride);

public sealed record PlatformSettingsAuditEntryDto(
    Guid Id,
    string CategoryKey,
    string Action,
    string? ActorDisplayName,
    string? Details,
    DateTimeOffset OccurredOnUtc);

public sealed record GeneralSettingsPayload(
    string StoreName,
    string StoreDescription,
    string ContactEmail,
    string SupportEmail,
    string Phone,
    string Address,
    string Timezone,
    string DefaultLanguage,
    string DefaultCurrency,
    string StoreStatus);

public sealed record BrandingSettingsPayload(
    string? LogoUrl,
    string? FaviconUrl,
    string? AdminLogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string? FooterLogoUrl,
    string BrowserTitle);

public sealed record StorefrontHeroSectionSettings(
    string Title,
    string Subtitle,
    string PrimaryButtonText,
    string PrimaryButtonUrl,
    string SecondaryButtonText,
    string SecondaryButtonUrl,
    string BackgroundImageUrl,
    string? OverlayImageUrl,
    double OverlayOpacity,
    string BadgeText,
    string BadgeColor,
    bool Visible);

public sealed record StorefrontPromotionalBannerSettings(
    string Title,
    string Subtitle,
    string ImageUrl,
    string ButtonText,
    string ButtonUrl,
    int CountdownSeconds,
    string? CountdownEndsAtUtc,
    string BackgroundColor,
    bool Visible);

public sealed record StorefrontFlashDealsSettings(
    string SectionTitle,
    string SectionSubtitle,
    bool CountdownEnabled,
    string? CountdownDateUtc,
    int MaximumProducts,
    string SortMethod,
    int CountdownSeconds,
    bool Visible);

public sealed record StorefrontFeaturedCollectionsSettings(
    string Title,
    string Subtitle,
    int MaximumItems,
    string DisplayStyle,
    bool Visible);

public sealed record StorefrontPopularCategoriesSettings(
    string SectionTitle,
    int MaximumCategories,
    string SortOrder,
    bool Visible);

public sealed record StorefrontFeaturedProductSettings(
    string? ProductId,
    string Badge,
    string CallToAction,
    bool Visible);

public sealed record StorefrontTrustItemSettings(
    string Id,
    string IconUrl,
    string Title,
    string Description,
    int SortOrder,
    bool Visible);

public sealed record StorefrontFooterSettings(
    string CompanyName,
    string Copyright,
    string SupportEmail,
    string SupportPhone,
    string Address,
    string WorkingHours,
    string? FacebookUrl,
    string? InstagramUrl,
    string? XUrl,
    string? DiscordUrl,
    string? TelegramUrl,
    string? YouTubeUrl,
    string? TikTokUrl,
    string? WhatsAppUrl);

public sealed record StorefrontSeoDefaultsSettings(
    string DefaultMetaTitle,
    string DefaultMetaDescription,
    string? OpenGraphImageUrl,
    string TwitterCard,
    string CanonicalUrl);

public sealed record StorefrontContentSettingsPayload(
    StorefrontHeroSectionSettings Hero,
    StorefrontPromotionalBannerSettings PromotionalBanner,
    StorefrontFlashDealsSettings FlashDeals,
    StorefrontFeaturedCollectionsSettings FeaturedCollections,
    StorefrontPopularCategoriesSettings PopularCategories,
    StorefrontFeaturedProductSettings FeaturedProduct,
    IReadOnlyList<StorefrontTrustItemSettings> TrustBar,
    StorefrontFooterSettings Footer,
    StorefrontSeoDefaultsSettings Seo);

public sealed record LocalizationSettingsPayload(
    string DefaultLanguage,
    string[] SupportedLanguages,
    bool RtlEnabled);

public sealed record CurrencySettingsPayload(
    string BaseCurrency,
    string[] SupportedCurrencies,
    int ExchangeRateRefreshMinutes,
    string Provider,
    string? ExternalApiUrl,
    Dictionary<string, decimal> StaticRates);

public sealed record ThemeSettingsPayload(
    string DefaultThemeId,
    bool AllowCustomerThemeSwitch,
    bool SyncWithSystemPreference);

public sealed record EmailSettingsPayload(
    bool Enabled,
    string SmtpHost,
    int SmtpPort,
    string? Username,
    string? Password,
    bool UseSsl,
    string SenderName,
    string SenderEmail,
    string ApplicationBaseUrl,
    string VerificationPath,
    string ResetPasswordPath,
    bool HasStoredPassword);

public sealed record AuthenticationSettingsPayload(
    int MinimumPasswordLength,
    bool RequireNumbers,
    bool RequireSymbols,
    bool RequireUppercase,
    int SessionTimeoutMinutes,
    int RememberMeDurationDays,
    bool AdminOtpEnabled);

public sealed record SecuritySettingsPayload(
    int MaxFailedAccessAttempts,
    int LockoutDurationMinutes,
    bool RequireEmailVerification,
    bool EnforceHttps,
    bool SecurityAlertsEnabled = true,
    string SecurityAlertMinSeverity = "High",
    int SecurityAlertThrottleMinutes = 15);

public sealed record OtpSettingsPayload(
    int CodeLength,
    int ExpirationMinutes,
    int MaxAttempts,
    int ResendCooldownSeconds,
    int LockoutMinutes);

public sealed record TotpSettingsPayload(
    bool Enabled,
    bool ArchitectureReady,
    string Issuer,
    int Digits,
    int PeriodSeconds);

public sealed record CommerceSettingsPayload(
    decimal TaxRatePercent,
    bool ShippingEnabledFuture,
    int ReservationTimeoutMinutes,
    int OrderExpirationHours,
    int RefundWindowDays,
    string InvoicePrefix,
    decimal DefaultSupplierMarginPercent = 20m);

public sealed record CheckoutSettingsPayload(
    bool GuestCheckoutAllowed,
    bool RequirePhoneNumber,
    string DefaultPaymentMethod,
    int CartAbandonmentMinutes);

public sealed record PromotionsSettingsPayload(
    decimal DefaultMaxDiscountPercent,
    bool StackCouponsAllowed,
    int DefaultCouponUsageLimit,
    int DefaultPerUserLimit);

public sealed record MembershipsSettingsPayload(
    bool Enabled,
    int DefaultTrialDays,
    bool AutoRenewDefault);

public sealed record ReferralSettingsPayload(
    bool Enabled,
    int PointsPerReferral,
    decimal PointValueUsd,
    int RewardExpiryDays);

public sealed record InventorySettingsPayload(
    int LowStockThreshold,
    int ReservationTimeoutMinutes,
    bool AutomaticReleaseEnabled,
    string CodeRevealPolicy);

public sealed record QueueSettingsPayload(
    int WorkerIntervalSeconds,
    int BatchSize,
    int MaxConcurrency,
    int VisibilityTimeoutSeconds);

public sealed record RetryPoliciesSettingsPayload(
    int RetryCount,
    int RetryDelaySeconds,
    int TimeoutSeconds,
    int DeadLetterThreshold,
    bool UseExponentialBackoff = false);

public sealed record NotificationsSettingsPayload(
    bool EmailEnabled,
    bool SmsEnabledFuture,
    bool PushEnabledFuture,
    bool AdminAlertsEnabled,
    bool LowStockAlertsEnabled,
    bool WorkerAlertsEnabled);

public sealed record MediaSettingsPayload(
    long MaxUploadSizeBytes,
    string[] AllowedContentTypes,
    bool ImageCompressionEnabled,
    int ThumbnailSmallPx,
    int ThumbnailMediumPx,
    int ThumbnailLargePx);

public sealed record SeoSettingsPayload(
    string MetaTitle,
    string MetaDescription,
    string OpenGraphImageUrl,
    string TwitterCardType,
    string RobotsDirective,
    string CanonicalBaseUrl);

public sealed record AnalyticsSettingsPayload(
    string? GoogleAnalyticsId,
    string? GoogleTagManagerId,
    string? MetaPixelId,
    bool EnableTracking);

public sealed record IntegrationsSettingsPayload(
    string? StripePublishableKey,
    string? StripeWebhookSecretConfigured,
    string? PayPalClientId,
    bool WebhooksEnabled);

public sealed record MaintenanceSettingsPayload(
    bool Enabled,
    string Message,
    string[] AllowedRoleNames,
    string? BypassPassword,
    bool HasStoredBypassPassword);

public sealed record LegalSettingsPayload(
    string TermsHtml,
    string PrivacyHtml,
    string RefundPolicyHtml,
    string SupportPolicyHtml);

public sealed record PublicPlatformSettingsDto(
    GeneralSettingsPayload General,
    BrandingSettingsPayload Branding,
    LocalizationSettingsPayload Localization,
    SeoSettingsPayload Seo,
    AnalyticsSettingsPayload Analytics,
    MaintenanceSettingsPayload Maintenance,
    LegalSettingsPayload Legal);
