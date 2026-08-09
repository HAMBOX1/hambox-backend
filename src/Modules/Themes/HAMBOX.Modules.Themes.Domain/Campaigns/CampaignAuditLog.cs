using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Campaigns;

/// <summary>
/// Mirrors <see cref="Themes.ThemeAuditLog"/> exactly, as a sibling table rather than a shared one —
/// keeping ThemeAuditLog.ThemeId's meaning unambiguous (it's never a campaign reference) while
/// staying instantly familiar to anyone who's already touched theme auditing.
/// </summary>
public sealed class CampaignAuditLog : BaseEntity
{
    private CampaignAuditLog()
    {
    }

    private CampaignAuditLog(Guid id, Guid campaignId, CampaignAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        CampaignId = campaignId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid CampaignId { get; private set; }
    public CampaignAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static CampaignAuditLog Create(Guid campaignId, CampaignAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), campaignId, action, actorUserId, detailsJson);
}
