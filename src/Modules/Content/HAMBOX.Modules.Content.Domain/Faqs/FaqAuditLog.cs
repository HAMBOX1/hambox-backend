using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Content.Domain.Faqs;

public sealed class FaqAuditLog : BaseEntity
{
    private FaqAuditLog()
    {
    }

    private FaqAuditLog(Guid id, Guid faqId, FaqAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        FaqId = faqId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid FaqId { get; private set; }
    public FaqAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static FaqAuditLog Create(Guid faqId, FaqAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), faqId, action, actorUserId, detailsJson);
}
