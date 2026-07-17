using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job: soft-deletes <see cref="Domain.Security.BlockedEmail"/> entries whose expiration
/// has passed. Permanent entries (<c>ExpiresOnUtc == null</c>) are never touched.
/// </summary>
internal sealed class CleanupExpiredEmailBansJobHandler(IBackgroundJobSerializer serializer, IIdentityDbContext dbContext)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => SecurityJobTypes.CleanupExpiredEmailBans;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await dbContext.BlockedEmails
            .Where(b => b.ExpiresOnUtc != null && b.ExpiresOnUtc <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            dbContext.BlockedEmails.RemoveRange(expired);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
