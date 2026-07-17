using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Domain.Legal;

namespace HAMBOX.Modules.Legal.Application.Services;

public static class LegalAuditWriter
{
    public static void Record(
        ILegalDbContext dbContext,
        Guid legalSectionId,
        LegalSectionAuditAction action,
        string? actorUserId,
        string? detailsJson = null)
    {
        dbContext.LegalSectionAuditLogs.Add(LegalSectionAuditLog.Create(legalSectionId, action, actorUserId, detailsJson));
    }
}
