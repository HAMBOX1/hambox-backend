using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;

internal sealed class ScheduledReportWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled report worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scheduledReports = scope.ServiceProvider.GetRequiredService<IScheduledReportService>();
                var db = scope.ServiceProvider.GetRequiredService<ICommerceDbContext>();

                var due = await scheduledReports.GetDueSchedulesAsync(stoppingToken);
                foreach (var schedule in due)
                {
                    var alreadyRunning = await db.ScheduledReportExecutions.AsNoTracking()
                        .AnyAsync(
                            x => x.ScheduledReportId == schedule.Id
                                 && (x.Status == ScheduledReportExecutionStatuses.Pending
                                     || x.Status == ScheduledReportExecutionStatuses.Running)
                                 && x.TriggeredBy == ScheduledReportTriggers.Schedule
                                 && x.StartedOnUtc > DateTimeOffset.UtcNow.AddHours(-1),
                            stoppingToken);

                    if (alreadyRunning)
                    {
                        continue;
                    }

                    var execution = ScheduledReportExecution.Create(schedule.Id, ScheduledReportTriggers.Schedule);
                    db.ScheduledReportExecutions.Add(execution);
                    await db.SaveChangesAsync(stoppingToken);

                    await scheduledReports.ExecuteAsync(schedule.Id, ScheduledReportTriggers.Schedule, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled report worker tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Scheduled report worker stopped");
    }
}
