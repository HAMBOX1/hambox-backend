using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemeAuditLog : BaseEntity
{
    private ThemeAuditLog()
    {
    }

    private ThemeAuditLog(Guid id, Guid themeId, ThemeAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        ThemeId = themeId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid ThemeId { get; private set; }
    public ThemeAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static ThemeAuditLog Create(Guid themeId, ThemeAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), themeId, action, actorUserId, detailsJson);
}
