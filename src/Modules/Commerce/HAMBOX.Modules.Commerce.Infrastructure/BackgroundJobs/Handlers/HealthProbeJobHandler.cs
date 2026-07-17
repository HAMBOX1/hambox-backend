using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class HealthProbeJobHandler(
    IBackgroundJobSerializer serializer,
    ISystemHealthService health,
    ICommerceDbContext db) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.HealthProbe;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var result = await health.GetHealthAsync(cancellationToken);
        foreach (var component in result.Components.Where(c => c.Status is "Unhealthy" or "Degraded"))
        {
            var code = $"HEALTH_{component.Name.Replace(' ', '_').ToUpperInvariant()}";
            var exists = await db.OperationalAlerts.AnyAsync(
                a => a.Code == code && !a.IsAcknowledged && a.CreatedOnUtc >= DateTimeOffset.UtcNow.AddHours(-1),
                cancellationToken);
            if (!exists)
            {
                db.OperationalAlerts.Add(OperationalAlert.Create(
                    code,
                    $"{component.Name} {component.Status}",
                    component.Detail ?? $"{component.Name} reported {component.Status}.",
                    component.Status == "Unhealthy"
                        ? OperationalAlertSeverity.Critical
                        : OperationalAlertSeverity.Warning));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
