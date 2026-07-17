using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Legal.Domain.Legal;

public sealed class LegalSectionAuditLog : BaseEntity
{
    private LegalSectionAuditLog()
    {
    }

    private LegalSectionAuditLog(Guid id, Guid legalSectionId, LegalSectionAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        LegalSectionId = legalSectionId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid LegalSectionId { get; private set; }
    public LegalSectionAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static LegalSectionAuditLog Create(Guid legalSectionId, LegalSectionAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), legalSectionId, action, actorUserId, detailsJson);
}
