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
    public const string PurchasePoints = "purchasePoints";
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
    public const string Features = "features";
    public const string Loyalty = "loyalty";

    public static readonly IReadOnlyList<string> All =
    [
        General, Branding, Storefront, Localization, Currency, Theme, Email,
        Authentication, Security, Otp, Totp, Commerce, Checkout, Promotions,
        Memberships, Referral, PurchasePoints, Inventory, Queue, RetryPolicies, Notifications,
        Media, Seo, Analytics, Integrations, Maintenance, Legal, Features, Loyalty,
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
    string StoreStatus,
    string[]? RegularProductViews = null,
    string? RegularDefaultProductView = null,
    string[]? EliteProductViews = null,
    string? EliteDefaultProductView = null);

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

public sealed record StorefrontNavLinkSettings(
    string Id,
    string LabelEn,
    string LabelAr,
    bool Visible,
    bool ShowInFooter = true);

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
    StorefrontSeoDefaultsSettings Seo,
    IReadOnlyList<StorefrontNavLinkSettings> NavigationLinks);

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
    bool AdminOtpEnabled,
    // Admin-portal-only (never applied to the customer/storefront auth context): an admin session
    // idle (no refresh activity) past AdminIdleTimeoutMinutes, or older than AdminMaxSessionLifetimeHours
    // regardless of activity, must re-authenticate with Email+Password+OTP.
    int AdminIdleTimeoutMinutes = 30,
    int AdminMaxSessionLifetimeHours = 8);

public sealed record SecuritySettingsPayload(
    int MaxFailedAccessAttempts,
    int LockoutDurationMinutes,
    bool RequireEmailVerification,
    bool SecurityAlertsEnabled = true,
    string SecurityAlertMinSeverity = "High",
    int SecurityAlertThrottleMinutes = 15,
    // When a customer must enter an e-mailed code before a purchased license key is shown in plaintext:
    // "Off" (never), "FirstReveal" (once per key, then not again) or "EveryReveal" (each time).
    string CustomerRevealOtpMode = "FirstReveal");

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
    decimal DefaultSupplierMarginPercent = 20m,
    /// <summary>Orders one customer may place within an hour before the next one is held for review.</summary>
    int VelocityOrdersPerHour = 5,
    /// <summary>Failed payments by one customer within 24 hours before the next order is held for review.</summary>
    int FailedPaymentsPerDay = 4);

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
    int RewardExpiryDays,
    int InviteeStars = 0,
    decimal MinOrderUsd = 0m,
    int HoldDays = 0,
    int MaxRewardedPerReferrer = 0,
    int MilestoneReferrals = 0,
    int MilestoneBonusStars = 0);

/// <summary>
/// Earn-only purchase points. <paramref name="PointsPerUsd"/> is applied to the eligible amount in USD (the
/// canonical stored order currency); <paramref name="AmountBasis"/> is <c>NetOfTax</c> (subtotal minus
/// discounts, excluding tax and gateway fees) or <c>Total</c> (everything the customer paid).
/// </summary>
public sealed record PurchasePointsSettingsPayload(
    bool Enabled,
    decimal PointsPerUsd,
    string AmountBasis,
    int HoldDays = 0,
    int ExpiryDays = 0,
    decimal RedeemStarsPerUsd = 100m,
    int RedeemMaxPercent = 50,
    int MinRedeemStars = 0,
    decimal MinMarginPercent = 0m,
    string StackingMode = StarsStackingMode.HighestOffer,
    decimal MaxMultiplier = 0m);

/// <summary>How several Stars rules that match the same item are combined (CM-03).</summary>
public static class StarsStackingMode
{
    /// <summary>Only the winning rule (highest priority) applies. The default: nothing stacks by accident.</summary>
    public const string HighestOffer = "HighestOffer";

    /// <summary>Multiplier bonuses add together (×2 and ×3 give ×4), fixed amounts add, an Exclude still wins; optionally capped by MaxMultiplier.</summary>
    public const string Combine = "Combine";
}

public static class PurchasePointsAmountBasis
{
    public const string NetOfTax = "NetOfTax";
    public const string Total = "Total";
}

public sealed record InventorySettingsPayload(
    int LowStockThreshold,
    int ReservationTimeoutMinutes,
    bool AutomaticReleaseEnabled,
    string CodeRevealPolicy,
    bool ShowLowStockBadge = true,
    bool ShowLowStockMessage = true);

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
    bool UseExponentialBackoff = false,
    string? RetryScheduleSeconds = null,
    bool UseJitter = true,
    /// <summary>Highest sustained request rate to any single supplier (0 = no spacing).</summary>
    double SupplierMaxRequestsPerSecond = 5,
    /// <summary>Pause after a supplier answers 429 without a usable Retry-After.</summary>
    int SupplierDefaultCooldownSeconds = 60,
    /// <summary>Longest pause a supplier's Retry-After can impose on us.</summary>
    int SupplierMaxCooldownSeconds = 900);

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

/// <summary>The Feature Center: every loyalty/membership feature ships dark and is switched on here (CORE-04).</summary>
public sealed record FeatureCenterSettingsPayload(
    bool StarsShop = false,
    bool LoyaltyRanks = false,
    bool RankThemes = false,
    bool EliteSubscription = false,
    bool EliteVault = false,
    bool GiftPass = false,
    bool ReferralGiveAndGet = false,
    bool StarsCampaigns = false,
    bool Missions = false,
    bool Watchlist = false,
    bool EliteBusiness = false,
    bool ProfileStyle = false);

/// <summary>How a customer's loyalty rank is earned (LV-02). Independent of the spendable Stars balance.</summary>
public sealed record LoyaltySettingsPayload(
    string Criterion = LoyaltyCriterion.SpendUsd,
    bool AllowDowngrade = false);

public static class LoyaltyCriterion
{
    public const string SpendUsd = "SpendUsd";
    public const string CompletedOrders = "CompletedOrders";
}

public sealed record PublicPlatformSettingsDto(
    GeneralSettingsPayload General,
    BrandingSettingsPayload Branding,
    LocalizationSettingsPayload Localization,
    SeoSettingsPayload Seo,
    AnalyticsSettingsPayload Analytics,
    MaintenanceSettingsPayload Maintenance,
    LegalSettingsPayload Legal,
    FeatureCenterSettingsPayload? Features = null);
