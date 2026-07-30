using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Content.Domain.LandingPages;

public sealed class LandingPageAuditLog : BaseEntity
{
    private LandingPageAuditLog()
    {
    }

    private LandingPageAuditLog(Guid id, Guid templateId, LandingPageAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        TemplateId = templateId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid TemplateId { get; private set; }
    public LandingPageAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static LandingPageAuditLog Create(Guid templateId, LandingPageAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), templateId, action, actorUserId, detailsJson);
}
