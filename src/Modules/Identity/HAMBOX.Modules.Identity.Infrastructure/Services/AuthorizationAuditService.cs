using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class AuthorizationAuditService(IdentityDbContext dbContext) : IAuthorizationAuditService
{
    public async Task RecordAsync(
        string action,
        string entityType,
        Guid? entityId,
        Guid actorUserId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var entry = AuthorizationAuditLog.Create(action, entityType, entityId, actorUserId, details, ipAddress);
        dbContext.AuthorizationAuditLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
