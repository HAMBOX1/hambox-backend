using System.Net.Mail;
using System.Text.Json;
using HAMBOX.Application.PlatformSettings;

namespace HAMBOX.Modules.Identity.Application.Features.PlatformSettings;

/// <summary>
/// Range / format checks for an incoming settings payload. Updates are merged over the defaults, so only the
/// fields actually present in the JSON are checked - a partial payload stays valid. Without these, a typo
/// such as a queue concurrency of 0 or a negative password length was saved and silently broke the platform.
/// </summary>
internal static class PlatformSettingsPayloadRules
{
    private static readonly IReadOnlyDictionary<string, (string Field, double Min, double Max)[]> NumericRules =
        new Dictionary<string, (string, double, double)[]>(StringComparer.Ordinal)
        {
            [PlatformSettingsCategoryKeys.Authentication] =
            [
                ("minimumPasswordLength", 6, 128), ("sessionTimeoutMinutes", 1, 10080), ("rememberMeDurationDays", 1, 365),
                ("adminIdleTimeoutMinutes", 1, 1440), ("adminMaxSessionLifetimeHours", 1, 168),
            ],
            [PlatformSettingsCategoryKeys.Security] =
            [
                ("maxFailedAccessAttempts", 1, 100), ("lockoutDurationMinutes", 1, 10080), ("securityAlertThrottleMinutes", 0, 1440),
            ],
            [PlatformSettingsCategoryKeys.Otp] =
            [
                ("codeLength", 4, 10), ("expirationMinutes", 1, 60), ("maxAttempts", 1, 20),
                ("resendCooldownSeconds", 0, 3600), ("lockoutMinutes", 1, 1440),
            ],
            [PlatformSettingsCategoryKeys.Totp] = [("digits", 6, 8), ("periodSeconds", 15, 120)],
            [PlatformSettingsCategoryKeys.Commerce] =
            [
                ("taxRatePercent", 0, 100), ("reservationTimeoutMinutes", 1, 1440), ("orderExpirationHours", 1, 8760),
                ("refundWindowDays", 0, 3650), ("defaultSupplierMarginPercent", 0, 1000),
                ("velocityOrdersPerHour", 1, 1000), ("failedPaymentsPerDay", 1, 1000),
            ],
            [PlatformSettingsCategoryKeys.Checkout] = [("cartAbandonmentMinutes", 1, 525600)],
            [PlatformSettingsCategoryKeys.Promotions] =
            [
                ("defaultMaxDiscountPercent", 0, 100), ("defaultCouponUsageLimit", 0, 10_000_000), ("defaultPerUserLimit", 0, 10_000_000),
            ],
            [PlatformSettingsCategoryKeys.Memberships] = [("defaultTrialDays", 0, 365)],
            [PlatformSettingsCategoryKeys.Referral] =
            [
                ("pointsPerReferral", 0, 10_000_000), ("pointValueUsd", 0, 10_000), ("rewardExpiryDays", 0, 3650),
            ],
            [PlatformSettingsCategoryKeys.PurchasePoints] = [("pointsPerUsd", 0, 10_000)],
            [PlatformSettingsCategoryKeys.Inventory] = [("lowStockThreshold", 0, 1_000_000), ("reservationTimeoutMinutes", 1, 1440)],
            [PlatformSettingsCategoryKeys.Queue] =
            [
                ("workerIntervalSeconds", 1, 3600), ("batchSize", 1, 1000), ("maxConcurrency", 1, 64), ("visibilityTimeoutSeconds", 10, 86400),
            ],
            [PlatformSettingsCategoryKeys.RetryPolicies] =
            [
                ("retryCount", 0, 20), ("retryDelaySeconds", 0, 86400), ("timeoutSeconds", 1, 3600), ("deadLetterThreshold", 1, 100),
                ("supplierMaxRequestsPerSecond", 0, 1000), ("supplierDefaultCooldownSeconds", 0, 86400), ("supplierMaxCooldownSeconds", 0, 86400),
            ],
            [PlatformSettingsCategoryKeys.Media] =
            [
                ("maxUploadSizeBytes", 1, 524_288_000), ("thumbnailSmallPx", 16, 4096), ("thumbnailMediumPx", 16, 4096), ("thumbnailLargePx", 16, 4096),
            ],
            [PlatformSettingsCategoryKeys.Email] = [("smtpPort", 1, 65535)],
        };

    private static readonly IReadOnlyDictionary<string, string[]> EmailFields =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [PlatformSettingsCategoryKeys.General] = ["contactEmail", "supportEmail"],
            [PlatformSettingsCategoryKeys.Email] = ["senderEmail"],
        };

    /// <summary>Returns a message describing the first invalid field, or <c>null</c> when the payload is acceptable.</summary>
    public static string? FindProblem(string categoryKey, string payloadJson)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return "The settings payload is not valid JSON.";
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return "The settings payload must be a JSON object.";
        }

        if (NumericRules.TryGetValue(categoryKey, out var rules))
        {
            foreach (var (field, min, max) in rules)
            {
                var property = FindProperty(root, field);
                if (property is null || property.Value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out var value))
                {
                    return $"'{field}' must be a number.";
                }

                if (value < min || value > max)
                {
                    return $"'{field}' must be between {min} and {max}.";
                }
            }
        }

        if (categoryKey == PlatformSettingsCategoryKeys.PurchasePoints
            && FindProperty(root, "amountBasis") is { ValueKind: not JsonValueKind.Null } basis
            && basis.ValueKind == JsonValueKind.String
            && basis.GetString() is not (PurchasePointsAmountBasis.NetOfTax or PurchasePointsAmountBasis.Total))
        {
            return $"'amountBasis' must be '{PurchasePointsAmountBasis.NetOfTax}' or '{PurchasePointsAmountBasis.Total}'.";
        }

        if (EmailFields.TryGetValue(categoryKey, out var emailFields))
        {
            foreach (var field in emailFields)
            {
                var property = FindProperty(root, field);
                var text = property is { ValueKind: JsonValueKind.String } p ? p.GetString() : null;
                if (!string.IsNullOrWhiteSpace(text) && !MailAddress.TryCreate(text, out _))
                {
                    return $"'{field}' must be a valid e-mail address.";
                }
            }
        }

        return null;
    }

    private static JsonElement? FindProperty(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }
}
