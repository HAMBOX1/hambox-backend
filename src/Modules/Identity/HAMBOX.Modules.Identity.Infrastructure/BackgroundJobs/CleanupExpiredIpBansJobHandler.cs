using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job: soft-deletes <see cref="Domain.Security.BlockedIp"/> entries whose expiration
/// has passed. Permanent entries (<c>ExpiresOnUtc == null</c>) are never touched.
/// </summary>
internal sealed class CleanupExpiredIpBansJobHandler(IBackgroundJobSerializer serializer, IIdentityDbContext dbContext)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => SecurityJobTypes.CleanupExpiredIpBans;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await dbContext.BlockedIps
            .Where(b => b.ExpiresOnUtc != null && b.ExpiresOnUtc <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            dbContext.BlockedIps.RemoveRange(expired);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
