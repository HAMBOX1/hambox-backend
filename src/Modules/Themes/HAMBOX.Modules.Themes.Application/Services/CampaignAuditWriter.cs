using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Domain.Campaigns;

namespace HAMBOX.Modules.Themes.Application.Services;

public static class CampaignAuditWriter
{
    public static void Record(
        IThemesDbContext dbContext,
        Guid campaignId,
        CampaignAuditAction action,
        string? actorUserId,
        string? detailsJson = null)
    {
        dbContext.CampaignAuditLogs.Add(CampaignAuditLog.Create(campaignId, action, actorUserId, detailsJson));
    }
}
