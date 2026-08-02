using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Communication;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class SecurityEventLogger(
    IdentityDbContext dbContext,
    IPlatformSettingsProvider platformSettings,
    ISecurityAlertRecipientResolver recipientResolver,
    ICommunicationService communicationService,
    IMemoryCache cache) : ISecurityEventLogger
{
    private const string ThrottleCacheKeyPrefix = "security-alert-throttle:";

    public async Task LogAsync(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? country = null,
        string? userAgent = null,
        string? correlationId = null,
        string? city = null,
        CancellationToken cancellationToken = default)
    {
        var entry = SecurityEventLog.Record(
            eventType, severity, description, actorUserId, targetUserId, ipAddress, country, userAgent, correlationId, city);

        dbContext.SecurityEventLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        await TrySendAlertAsync(entry, eventType, severity, description, targetUserId, ipAddress, cancellationToken);
    }

    /// <summary>
    /// Emails Owners/alert-permission holders for High+ severity events, throttled per
    /// event-type+target so a burst (e.g. repeated failed logins) sends one email per window
    /// instead of one per attempt. One hook here covers every <see cref="ISecurityEventLogger"/>
    /// call site (login failures, blocks, permission denials, ...) rather than wiring alerting
    /// into each of them individually.
    /// </summary>
    private async Task TrySendAlertAsync(
        SecurityEventLog entry,
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? targetUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var security = await platformSettings.GetSecurityAsync(cancellationToken);
        if (!security.SecurityAlertsEnabled)
        {
            return;
        }

        if (!Enum.TryParse<SecurityEventSeverity>(security.SecurityAlertMinSeverity, ignoreCase: true, out var minSeverity))
        {
            minSeverity = SecurityEventSeverity.High;
        }

        if (severity < minSeverity)
        {
            return;
        }

        var throttleKey = $"{ThrottleCacheKeyPrefix}{eventType}:{targetUserId?.ToString() ?? ipAddress ?? "unknown"}";
        if (cache.TryGetValue(throttleKey, out _))
        {
            return;
        }

        cache.Set(throttleKey, true, TimeSpan.FromMinutes(Math.Max(1, security.SecurityAlertThrottleMinutes)));

        var recipientUserIds = await recipientResolver.GetRecipientUserIdsAsync(cancellationToken);
        var variables = new Dictionary<string, string>
        {
            ["EventType"] = eventType.ToString(),
            ["Severity"] = severity.ToString(),
            ["Description"] = description,
            ["IpAddress"] = ipAddress ?? "unknown",
            ["OccurredOnUtc"] = entry.OccurredOnUtc.ToString("u"),
        };

        var priority = severity == SecurityEventSeverity.Critical ? CommunicationPriority.Critical : CommunicationPriority.High;

        foreach (var recipientUserId in recipientUserIds)
        {
            await communicationService.SendAsync(new CommunicationRequest(
                UserId: recipientUserId.ToString(),
                TemplateKey: SecurityTemplateKeys.SecurityAlert,
                Category: CommunicationCategory.Security,
                Variables: variables,
                Priority: priority,
                RelatedEntityType: "SecurityEventLog",
                RelatedEntityId: entry.Id.ToString()), cancellationToken);
        }
    }
}
