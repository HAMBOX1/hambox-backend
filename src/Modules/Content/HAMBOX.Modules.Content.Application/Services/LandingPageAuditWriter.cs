using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Domain.LandingPages;

namespace HAMBOX.Modules.Content.Application.Services;

public static class LandingPageAuditWriter
{
    public static void Record(
        IContentDbContext dbContext,
        Guid templateId,
        LandingPageAuditAction action,
        string? actorUserId,
        string? detailsJson = null)
    {
        dbContext.LandingPageAuditLogs.Add(LandingPageAuditLog.Create(templateId, action, actorUserId, detailsJson));
    }
}
