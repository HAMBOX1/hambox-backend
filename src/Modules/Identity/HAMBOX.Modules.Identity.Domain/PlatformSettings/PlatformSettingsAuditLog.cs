using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.PlatformSettings;

public sealed class PlatformSettingsAuditLog : Entity
{
    private PlatformSettingsAuditLog()
    {
    }

    private PlatformSettingsAuditLog(
        Guid id,
        string categoryKey,
        string action,
        string? actorUserId,
        string? actorDisplayName,
        string? details)
        : base(id)
    {
        CategoryKey = categoryKey;
        Action = action;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        Details = details;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public string CategoryKey { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string? ActorUserId { get; private set; }

    public string? ActorDisplayName { get; private set; }

    public string? Details { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static PlatformSettingsAuditLog Create(
        string categoryKey,
        string action,
        string? actorUserId,
        string? actorDisplayName,
        string? details = null) =>
        new(Guid.NewGuid(), categoryKey, action, actorUserId, actorDisplayName, details);
}
