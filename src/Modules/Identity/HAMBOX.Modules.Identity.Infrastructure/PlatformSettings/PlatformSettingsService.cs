using System.Text.Json;
using System.Text.Json.Nodes;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.PlatformSettings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace HAMBOX.Modules.Identity.Infrastructure.PlatformSettings;

internal sealed class PlatformSettingsService(
    IIdentityDbContext dbContext,
    IConfiguration configuration,
    IMemoryCache memoryCache,
    IPlatformSettingsSecretProtector secretProtector,
    IPasswordHasher passwordHasher,
    ILogger<PlatformSettingsService> logger) : IPlatformSettingsService
{
    private const string CachePrefix = "platform-settings:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<PlatformSettingsCategoryDto>> GetAllCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var persisted = await dbContext.PlatformSettingsCategories
            .AsNoTracking()
            .ToDictionaryAsync(x => x.CategoryKey, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return PlatformSettingsCategoryMetadata.All
            .Select(meta =>
            {
                persisted.TryGetValue(meta.Key, out var row);
                var payload = DeserializeForAdmin(meta.Key, GetMergedJson(meta.Key, row?.PayloadJson));
                return new PlatformSettingsCategoryDto(
                    meta.Key,
                    meta.Label,
                    meta.Group,
                    payload,
                    row?.ModifiedOnUtc,
                    row is not null);
            })
            .ToList();
    }

    public async Task<PlatformSettingsCategoryDto> GetCategoryAsync(
        string categoryKey,
        CancellationToken cancellationToken = default)
    {
        var meta = PlatformSettingsCategoryMetadata.GetRequired(categoryKey);
        var row = await dbContext.PlatformSettingsCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CategoryKey == categoryKey, cancellationToken);

        var payload = DeserializeForAdmin(categoryKey, GetMergedJson(categoryKey, row?.PayloadJson));
        return new PlatformSettingsCategoryDto(
            meta.Key,
            meta.Label,
            meta.Group,
            payload,
            row?.ModifiedOnUtc,
            row is not null);
    }

    public async Task<PlatformSettingsCategoryDto> UpdateCategoryAsync(
        string categoryKey,
        string payloadJson,
        string? actorUserId,
        string? actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        PlatformSettingsCategoryMetadata.GetRequired(categoryKey);
        ValidateCategoryKey(categoryKey);

        var persistedJson = await PreparePersistedJsonAsync(categoryKey, payloadJson, cancellationToken);
        var row = await dbContext.PlatformSettingsCategories
            .FirstOrDefaultAsync(x => x.CategoryKey == categoryKey, cancellationToken);

        if (row is null)
        {
            row = PlatformSettingsCategory.Create(categoryKey, persistedJson);
            dbContext.PlatformSettingsCategories.Add(row);
        }
        else
        {
            row.UpdatePayload(persistedJson, actorUserId);
        }

        dbContext.PlatformSettingsAuditLogs.Add(
            PlatformSettingsAuditLog.Create(categoryKey, "Updated", actorUserId, actorDisplayName));

        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache(categoryKey);

        return await GetCategoryAsync(categoryKey, cancellationToken);
    }

    public async Task<PlatformSettingsCategoryDto> RestoreDefaultsAsync(
        string categoryKey,
        string? actorUserId,
        string? actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        PlatformSettingsCategoryMetadata.GetRequired(categoryKey);
        ValidateCategoryKey(categoryKey);

        var row = await dbContext.PlatformSettingsCategories
            .FirstOrDefaultAsync(x => x.CategoryKey == categoryKey, cancellationToken);

        if (row is not null)
        {
            dbContext.PlatformSettingsCategories.Remove(row);
        }

        dbContext.PlatformSettingsAuditLogs.Add(
            PlatformSettingsAuditLog.Create(categoryKey, "RestoredDefaults", actorUserId, actorDisplayName));

        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache(categoryKey);

        return await GetCategoryAsync(categoryKey, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformSettingsAuditEntryDto>> GetAuditLogAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        take = take is <= 0 or > 200 ? 50 : take;

        try
        {
            return await dbContext.PlatformSettingsAuditLogs
                .AsNoTracking()
                .OrderByDescending(x => x.OccurredOnUtc)
                .Take(take)
                .Select(x => new PlatformSettingsAuditEntryDto(
                    x.Id,
                    x.CategoryKey,
                    x.Action,
                    x.ActorDisplayName,
                    x.Details,
                    x.OccurredOnUtc))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to load platform settings audit log.");
            return Array.Empty<PlatformSettingsAuditEntryDto>();
        }
    }

    public async Task SendTestEmailAsync(string testRecipient, CancellationToken cancellationToken = default)
    {
        var email = await GetEmailForDeliveryAsync(cancellationToken);
        if (!email.Enabled)
        {
            throw new InvalidOperationException("SMTP delivery is disabled in platform settings.");
        }

        if (string.IsNullOrWhiteSpace(email.SmtpHost))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.SenderName, email.SenderEmail));
        message.To.Add(MailboxAddress.Parse(testRecipient));
        message.Subject = "HAMBOX SMTP Test";
        message.Body = new TextPart("plain")
        {
            Text = "This is a test email from HAMBOX platform settings.",
        };

        using var client = new SmtpClient { Timeout = 30_000 };
        var socketOptions = email.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(email.SmtpHost, email.SmtpPort, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(email.Username))
        {
            var password = await ResolveEmailPasswordAsync(cancellationToken);
            await client.AuthenticateAsync(email.Username, password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("SMTP test email sent to {Recipient}", testRecipient);
    }

    public async Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default)
    {
        var json = await GetMergedJsonCachedAsync(categoryKey, cancellationToken);
        return PlatformSettingsDefaultsFactory.Deserialize<T>(json);
    }

    public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) =>
        GetAsync<GeneralSettingsPayload>(PlatformSettingsCategoryKeys.General, cancellationToken);

    public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) =>
        GetAsync<BrandingSettingsPayload>(PlatformSettingsCategoryKeys.Branding, cancellationToken);

    public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) =>
        GetAsync<EmailSettingsPayload>(PlatformSettingsCategoryKeys.Email, cancellationToken);

    public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) =>
        GetAsync<AuthenticationSettingsPayload>(PlatformSettingsCategoryKeys.Authentication, cancellationToken);

    public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SecuritySettingsPayload>(PlatformSettingsCategoryKeys.Security, cancellationToken);

    public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) =>
        GetAsync<OtpSettingsPayload>(PlatformSettingsCategoryKeys.Otp, cancellationToken);

    public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        GetAsync<InventorySettingsPayload>(PlatformSettingsCategoryKeys.Inventory, cancellationToken);

    public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CurrencySettingsPayload>(PlatformSettingsCategoryKeys.Currency, cancellationToken);

    public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) =>
        GetAsync<MediaSettingsPayload>(PlatformSettingsCategoryKeys.Media, cancellationToken);

    public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) =>
        GetAsync<MaintenanceSettingsPayload>(PlatformSettingsCategoryKeys.Maintenance, cancellationToken);

    public async Task<bool> VerifyMaintenanceBypassPasswordAsync(
        string password,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.PlatformSettingsCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CategoryKey == PlatformSettingsCategoryKeys.Maintenance, cancellationToken);

        var hash = row is null
            ? null
            : (JsonNode.Parse(row.PayloadJson) as JsonObject)?["bypassPasswordHash"]?.GetValue<string>();

        return !string.IsNullOrWhiteSpace(hash) && passwordHasher.VerifyPassword(hash, password);
    }

    public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) =>
        GetAsync<StorefrontContentSettingsPayload>(PlatformSettingsCategoryKeys.Storefront, cancellationToken);

    public async Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default)
    {
        var general = await GetGeneralAsync(cancellationToken);
        var branding = await GetBrandingAsync(cancellationToken);
        var localization = await GetAsync<LocalizationSettingsPayload>(
            PlatformSettingsCategoryKeys.Localization, cancellationToken);
        var seo = await GetAsync<SeoSettingsPayload>(PlatformSettingsCategoryKeys.Seo, cancellationToken);
        var analytics = await GetAsync<AnalyticsSettingsPayload>(
            PlatformSettingsCategoryKeys.Analytics, cancellationToken);
        var maintenance = await GetMaintenanceAsync(cancellationToken);
        var legal = await GetAsync<LegalSettingsPayload>(PlatformSettingsCategoryKeys.Legal, cancellationToken);

        return new PublicPlatformSettingsDto(
            general, branding, localization, seo, analytics, maintenance, legal);
    }

    public void InvalidateCache(string? categoryKey = null)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            foreach (var key in PlatformSettingsCategoryKeys.All)
            {
                memoryCache.Remove(CachePrefix + key);
            }

            memoryCache.Remove(CachePrefix + "public");
            return;
        }

        memoryCache.Remove(CachePrefix + categoryKey);
        memoryCache.Remove(CachePrefix + "public");
    }

    public async Task<EmailSettings> GetEmailSettingsForLegacyAsync(CancellationToken cancellationToken = default)
    {
        var email = await GetEmailForDeliveryAsync(cancellationToken);
        var password = await ResolveEmailPasswordAsync(cancellationToken);
        return new EmailSettings
        {
            Enabled = email.Enabled,
            SmtpHost = email.SmtpHost,
            SmtpPort = email.SmtpPort,
            UseSsl = email.UseSsl,
            Username = email.Username,
            Password = password,
            FromAddress = email.SenderEmail,
            FromName = email.SenderName,
            ApplicationBaseUrl = email.ApplicationBaseUrl,
            VerificationPath = email.VerificationPath,
            ResetPasswordPath = email.ResetPasswordPath,
        };
    }

    private async Task<string> GetMergedJsonCachedAsync(string categoryKey, CancellationToken cancellationToken)
    {
        return await memoryCache.GetOrCreateAsync(CachePrefix + categoryKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var row = await dbContext.PlatformSettingsCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CategoryKey == categoryKey, cancellationToken);
            return GetMergedJson(categoryKey, row?.PayloadJson);
        }) ?? PlatformSettingsDefaultsFactory.GetDefaultJson(categoryKey, configuration);
    }

    private string GetMergedJson(string categoryKey, string? persistedJson)
    {
        var defaultsJson = PlatformSettingsDefaultsFactory.GetDefaultJson(categoryKey, configuration);
        if (string.IsNullOrWhiteSpace(persistedJson))
        {
            return defaultsJson;
        }

        var defaultsNode = JsonNode.Parse(defaultsJson) as JsonObject ?? new JsonObject();
        var persistedNode = JsonNode.Parse(persistedJson) as JsonObject ?? new JsonObject();

        foreach (var property in persistedNode)
        {
            if (property.Key.Equals("passwordCipher", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            defaultsNode[property.Key] = property.Value?.DeepClone();
        }

        return defaultsNode.ToJsonString(JsonOptions);
    }

    private async Task<string> PreparePersistedJsonAsync(
        string categoryKey,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        if (categoryKey == PlatformSettingsCategoryKeys.Email)
        {
            var incoming = PlatformSettingsDefaultsFactory.Deserialize<EmailSettingsPayload>(payloadJson);
            var existingRow = await dbContext.PlatformSettingsCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CategoryKey == categoryKey, cancellationToken);

            string? cipher = null;
            if (!string.IsNullOrWhiteSpace(incoming.Password))
            {
                cipher = secretProtector.Protect(incoming.Password);
            }
            else if (existingRow is not null)
            {
                var existingNode = JsonNode.Parse(existingRow.PayloadJson) as JsonObject;
                cipher = existingNode?["passwordCipher"]?.GetValue<string>();
            }
            else
            {
                var bootstrapPassword = configuration["EmailSettings:Password"];
                if (!string.IsNullOrWhiteSpace(bootstrapPassword))
                {
                    cipher = secretProtector.Protect(bootstrapPassword);
                }
            }

            var node = JsonNode.Parse(payloadJson) as JsonObject ?? new JsonObject();
            node.Remove("password");
            if (!string.IsNullOrWhiteSpace(cipher))
            {
                node["passwordCipher"] = cipher;
            }

            return node.ToJsonString(JsonOptions);
        }

        if (categoryKey == PlatformSettingsCategoryKeys.Maintenance)
        {
            var incoming = PlatformSettingsDefaultsFactory.Deserialize<MaintenanceSettingsPayload>(payloadJson);
            var existingRow = await dbContext.PlatformSettingsCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CategoryKey == categoryKey, cancellationToken);

            string? hash = null;
            if (!string.IsNullOrWhiteSpace(incoming.BypassPassword))
            {
                hash = passwordHasher.HashPassword(incoming.BypassPassword);
            }
            else if (existingRow is not null)
            {
                var existingNode = JsonNode.Parse(existingRow.PayloadJson) as JsonObject;
                hash = existingNode?["bypassPasswordHash"]?.GetValue<string>();
            }

            var node = JsonNode.Parse(payloadJson) as JsonObject ?? new JsonObject();
            node.Remove("bypassPassword");
            if (!string.IsNullOrWhiteSpace(hash))
            {
                node["bypassPasswordHash"] = hash;
            }

            return node.ToJsonString(JsonOptions);
        }

        return payloadJson;
    }

    private object DeserializeForAdmin(string categoryKey, string mergedJson)
    {
        if (categoryKey == PlatformSettingsCategoryKeys.Email)
        {
            var email = PlatformSettingsDefaultsFactory.Deserialize<EmailSettingsPayload>(mergedJson);
            var node = JsonNode.Parse(mergedJson) as JsonObject;
            var hasCipher = node?.ContainsKey("passwordCipher") == true
                || !string.IsNullOrWhiteSpace(configuration["EmailSettings:Password"]);
            return email with { Password = null, HasStoredPassword = hasCipher };
        }

        if (categoryKey == PlatformSettingsCategoryKeys.Maintenance)
        {
            var maintenance = PlatformSettingsDefaultsFactory.Deserialize<MaintenanceSettingsPayload>(mergedJson);
            var node = JsonNode.Parse(mergedJson) as JsonObject;
            var hasHash = node?.ContainsKey("bypassPasswordHash") == true;
            return maintenance with { BypassPassword = null, HasStoredBypassPassword = hasHash };
        }

        return PlatformSettingsDefaultsFactory.GetDefaultPayload(categoryKey, configuration).GetType() switch
        {
            var t when t == typeof(GeneralSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<GeneralSettingsPayload>(mergedJson),
            var t when t == typeof(BrandingSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<BrandingSettingsPayload>(mergedJson),
            var t when t == typeof(StorefrontContentSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<StorefrontContentSettingsPayload>(mergedJson),
            var t when t == typeof(LocalizationSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<LocalizationSettingsPayload>(mergedJson),
            var t when t == typeof(CurrencySettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<CurrencySettingsPayload>(mergedJson),
            var t when t == typeof(ThemeSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<ThemeSettingsPayload>(mergedJson),
            var t when t == typeof(AuthenticationSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<AuthenticationSettingsPayload>(mergedJson),
            var t when t == typeof(SecuritySettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<SecuritySettingsPayload>(mergedJson),
            var t when t == typeof(OtpSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<OtpSettingsPayload>(mergedJson),
            var t when t == typeof(TotpSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<TotpSettingsPayload>(mergedJson),
            var t when t == typeof(CommerceSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<CommerceSettingsPayload>(mergedJson),
            var t when t == typeof(CheckoutSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<CheckoutSettingsPayload>(mergedJson),
            var t when t == typeof(PromotionsSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<PromotionsSettingsPayload>(mergedJson),
            var t when t == typeof(MembershipsSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<MembershipsSettingsPayload>(mergedJson),
            var t when t == typeof(ReferralSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<ReferralSettingsPayload>(mergedJson),
            var t when t == typeof(InventorySettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<InventorySettingsPayload>(mergedJson),
            var t when t == typeof(QueueSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<QueueSettingsPayload>(mergedJson),
            var t when t == typeof(RetryPoliciesSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<RetryPoliciesSettingsPayload>(mergedJson),
            var t when t == typeof(NotificationsSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<NotificationsSettingsPayload>(mergedJson),
            var t when t == typeof(MediaSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<MediaSettingsPayload>(mergedJson),
            var t when t == typeof(SeoSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<SeoSettingsPayload>(mergedJson),
            var t when t == typeof(AnalyticsSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<AnalyticsSettingsPayload>(mergedJson),
            var t when t == typeof(IntegrationsSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<IntegrationsSettingsPayload>(mergedJson),
            var t when t == typeof(MaintenanceSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<MaintenanceSettingsPayload>(mergedJson),
            var t when t == typeof(LegalSettingsPayload) => PlatformSettingsDefaultsFactory.Deserialize<LegalSettingsPayload>(mergedJson),
            _ => JsonSerializer.Deserialize<JsonElement>(mergedJson, JsonOptions)!,
        };
    }

    private async Task<EmailSettingsPayload> GetEmailForDeliveryAsync(CancellationToken cancellationToken)
    {
        var json = await GetMergedJsonCachedAsync(PlatformSettingsCategoryKeys.Email, cancellationToken);
        return PlatformSettingsDefaultsFactory.Deserialize<EmailSettingsPayload>(json);
    }

    private async Task<string?> ResolveEmailPasswordAsync(CancellationToken cancellationToken)
    {
        var row = await dbContext.PlatformSettingsCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CategoryKey == PlatformSettingsCategoryKeys.Email, cancellationToken);

        if (row is not null)
        {
            var node = JsonNode.Parse(row.PayloadJson) as JsonObject;
            var cipher = node?["passwordCipher"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(cipher))
            {
                return secretProtector.Unprotect(cipher);
            }
        }

        return configuration["EmailSettings:Password"];
    }

    private static void ValidateCategoryKey(string categoryKey)
    {
        if (!PlatformSettingsCategoryKeys.All.Contains(categoryKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown settings category: {categoryKey}", nameof(categoryKey));
        }
    }
}
