using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Domain.Faqs;

namespace HAMBOX.Modules.Content.Application.Services;

public static class FaqAuditWriter
{
    public static void Record(
        IContentDbContext dbContext,
        Guid faqId,
        FaqAuditAction action,
        string? actorUserId,
        string? detailsJson = null)
    {
        dbContext.FaqAuditLogs.Add(FaqAuditLog.Create(faqId, action, actorUserId, detailsJson));
    }
}
