using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>Shared "don't re-raise the same alert within a window" upsert used by several job handlers.</summary>
internal static class OperationalAlertUpsert
{
    /// <param name="relatedEntityType">
    /// When supplied together with <paramref name="relatedEntityId"/>, the "already active" check is
    /// scoped to this specific entity rather than to <paramref name="code"/> alone — so independent
    /// incidents of the same alert type (e.g. two different stuck orders) each get their own
    /// deduplicated alert instead of collapsing into one aggregate row. Existing callers that omit
    /// these keep the original code-only, aggregate-style dedup unchanged.
    /// </param>
    public static async Task UpsertAsync(
        ICommerceDbContext db,
        string code,
        string title,
        string message,
        OperationalAlertSeverity severity,
        CancellationToken cancellationToken,
        string? relatedEntityType = null,
        string? relatedEntityId = null)
    {
        var query = db.OperationalAlerts.Where(
            a => a.Code == code && !a.IsAcknowledged && a.CreatedOnUtc >= DateTimeOffset.UtcNow.AddHours(-6));

        if (relatedEntityId is not null)
        {
            query = query.Where(a => a.RelatedEntityId == relatedEntityId);
        }

        var exists = await query.AnyAsync(cancellationToken);

        if (!exists)
        {
            db.OperationalAlerts.Add(OperationalAlert.Create(code, title, message, severity, relatedEntityType, relatedEntityId));
        }
    }
}
