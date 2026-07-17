using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job: restores users whose temporary block (<c>UserStatus.Blocked</c> with a
/// non-null <c>BlockExpiresOnUtc</c>) has expired back to Active. Permanent bans
/// (<c>UserStatus.Banned</c>) and suspensions never auto-expire and are unaffected.
/// </summary>
internal sealed class CleanupExpiredUserBlocksJobHandler(
    IBackgroundJobSerializer serializer,
    IIdentityDbContext dbContext,
    ISecurityEventLogger securityEventLogger) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => SecurityJobTypes.CleanupExpiredUserBlocks;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await dbContext.Users
            .Where(u => u.Status == UserStatus.Blocked && u.BlockExpiresOnUtc != null && u.BlockExpiresOnUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var user in expired)
        {
            user.Unblock();
            await securityEventLogger.LogAsync(
                SecurityEventType.AdminUnlock,
                SecurityEventSeverity.Low,
                $"Temporary block for user {user.Email} expired and was automatically lifted.",
                targetUserId: user.Id,
                cancellationToken: cancellationToken);
        }

        if (expired.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
