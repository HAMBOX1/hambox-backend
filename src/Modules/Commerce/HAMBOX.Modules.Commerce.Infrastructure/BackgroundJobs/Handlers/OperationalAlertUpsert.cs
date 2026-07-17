using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>Shared "don't re-raise the same alert within a window" upsert used by several job handlers.</summary>
internal static class OperationalAlertUpsert
{
    public static async Task UpsertAsync(
        ICommerceDbContext db,
        string code,
        string title,
        string message,
        OperationalAlertSeverity severity,
        CancellationToken cancellationToken)
    {
        var exists = await db.OperationalAlerts.AnyAsync(
            a => a.Code == code && !a.IsAcknowledged && a.CreatedOnUtc >= DateTimeOffset.UtcNow.AddHours(-6),
            cancellationToken);

        if (!exists)
        {
            db.OperationalAlerts.Add(OperationalAlert.Create(code, title, message, severity));
        }
    }
}
