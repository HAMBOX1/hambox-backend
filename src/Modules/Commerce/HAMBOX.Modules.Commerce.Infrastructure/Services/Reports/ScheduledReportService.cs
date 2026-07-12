using System.Text.Json;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Reports;
using HAMBOX.Modules.Commerce.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;

internal sealed class ScheduledReportService(
    ICommerceDbContext db,
    IReportBuilderService reportBuilder,
    IReportDocumentGenerator documentGenerator,
    ILogger<ScheduledReportService> logger) : IScheduledReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IReadOnlyList<ScheduledReportDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await db.ScheduledReports.AsNoTracking()
            .OrderByDescending(x => x.CreatedOnUtc)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<ScheduledReportDto> CreateAsync(
        string reportType,
        string? filtersJson,
        string format,
        string frequency,
        IReadOnlyList<string> emailRecipients,
        bool isEnabled,
        string? createdByUserId,
        Guid? reportDefinitionId = null,
        CancellationToken cancellationToken = default)
    {
        var entity = ScheduledReport.Create(
            reportType,
            filtersJson,
            format,
            frequency,
            JsonSerializer.Serialize(emailRecipients ?? [], JsonOptions),
            isEnabled,
            createdByUserId,
            reportDefinitionId);

        db.ScheduledReports.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ScheduledReportDto?> UpdateAsync(
        Guid id,
        string? filtersJson,
        string format,
        string frequency,
        IReadOnlyList<string> emailRecipients,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Update(
            filtersJson,
            format,
            frequency,
            JsonSerializer.Serialize(emailRecipients ?? [], JsonOptions),
            isEnabled);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.ScheduledReports.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Guid> EnqueueManualRunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await db.ScheduledReports.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Scheduled report not found.");
        }

        var execution = ScheduledReportExecution.Create(id, ScheduledReportTriggers.Manual);
        db.ScheduledReportExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

        // Execute immediately for manual runs so UI history updates without waiting for worker tick.
        await ExecuteAsync(id, ScheduledReportTriggers.Manual, cancellationToken);
        return execution.Id;
    }

    public async Task<IReadOnlyList<ScheduledReportDto>> GetDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await db.ScheduledReports.AsNoTracking()
            .Where(x => x.IsEnabled && x.NextRunOnUtc != null && x.NextRunOnUtc <= now)
            .OrderBy(x => x.NextRunOnUtc)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task ExecuteAsync(
        Guid scheduledReportId,
        string triggeredBy,
        CancellationToken cancellationToken = default)
    {
        var schedule = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == scheduledReportId, cancellationToken);
        if (schedule is null)
        {
            return;
        }

        var execution = await db.ScheduledReportExecutions
            .Where(x => x.ScheduledReportId == scheduledReportId
                        && (x.Status == ScheduledReportExecutionStatuses.Pending
                            || x.Status == ScheduledReportExecutionStatuses.Running)
                        && x.TriggeredBy == triggeredBy)
            .OrderByDescending(x => x.StartedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (execution is null)
        {
            execution = ScheduledReportExecution.Create(scheduledReportId, triggeredBy);
            db.ScheduledReportExecutions.Add(execution);
        }

        execution.MarkRunning();
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var filters = DeserializeFilters(schedule.FiltersJson);
            var model = await reportBuilder.BuildAsync(schedule.ReportType, filters, cancellationToken);
            var document = await documentGenerator.GenerateAsync(
                new ReportDocumentRequest(model, schedule.Format),
                cancellationToken);

            var userId = schedule.CreatedByUserId ?? "system";
            var download = ReportDownload.Create(
                userId,
                schedule.ReportType,
                schedule.Format,
                schedule.FiltersJson,
                document.FileName,
                document.Content.LongLength,
                correlationId: execution.Id.ToString("N"));

            db.ReportDownloads.Add(download);
            execution.MarkSucceeded(download.Id);
            schedule.MarkRun(DateTimeOffset.UtcNow);

            var recipients = DeserializeRecipients(schedule.EmailRecipientsJson);
            if (recipients.Count > 0)
            {
                logger.LogInformation(
                    "Scheduled report {ScheduleId} generated download {DownloadId}; email delivery is best-effort and not attached ({RecipientCount} recipients configured).",
                    schedule.Id,
                    download.Id,
                    recipients.Count);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled report {ScheduleId} execution failed", scheduledReportId);
            execution.MarkFailed(ex.Message);
            schedule.MarkRun(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ScheduledReportExecutionDto>> GetExecutionsAsync(
        Guid scheduledReportId,
        CancellationToken cancellationToken = default)
    {
        var items = await db.ScheduledReportExecutions.AsNoTracking()
            .Where(x => x.ScheduledReportId == scheduledReportId)
            .OrderByDescending(x => x.StartedOnUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return items.Select(x => new ScheduledReportExecutionDto(
            x.Id,
            x.ScheduledReportId,
            x.Status,
            x.StartedOnUtc,
            x.FinishedOnUtc,
            x.Error,
            x.DownloadId,
            x.TriggeredBy)).ToList();
    }

    private static ReportFilterRequest DeserializeFilters(string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
        {
            return new ReportFilterRequest("Last30", null, null, null, null, null, null, null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<ReportFilterRequest>(filtersJson, JsonOptions)
                   ?? new ReportFilterRequest("Last30", null, null, null, null, null, null, null, null);
        }
        catch
        {
            return new ReportFilterRequest("Last30", null, null, null, null, null, null, null, null);
        }
    }

    private static IReadOnlyList<string> DeserializeRecipients(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static ScheduledReportDto Map(ScheduledReport entity) =>
        new(
            entity.Id,
            entity.ReportDefinitionId,
            entity.ReportType,
            entity.FiltersJson,
            entity.Format,
            entity.Frequency,
            DeserializeRecipients(entity.EmailRecipientsJson),
            entity.IsEnabled,
            entity.NextRunOnUtc,
            entity.LastRunOnUtc,
            entity.CreatedByUserId,
            entity.CreatedOnUtc);
}
