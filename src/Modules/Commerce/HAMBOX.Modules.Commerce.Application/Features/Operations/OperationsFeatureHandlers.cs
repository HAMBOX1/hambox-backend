using System.Globalization;
using System.Text;
using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Operations;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Operations;

public sealed record GetOperationsDashboardQuery : IRequest<Result<OperationsDashboardDto>>;

internal sealed class GetOperationsDashboardQueryHandler(IOperationsMonitorService monitor)
    : IRequestHandler<GetOperationsDashboardQuery, Result<OperationsDashboardDto>>
{
    public async Task<Result<OperationsDashboardDto>> Handle(
        GetOperationsDashboardQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await monitor.GetDashboardAsync(cancellationToken));
}

public sealed record GetOperationalJobsQuery(
    string? Status,
    string? JobType,
    string? Search,
    string? Queue,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int Page,
    int PageSize,
    string? Sort) : IRequest<Result<PagedResult<OperationalJobDto>>>;

internal sealed class GetOperationalJobsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetOperationalJobsQuery, Result<PagedResult<OperationalJobDto>>>
{
    public async Task<Result<PagedResult<OperationalJobDto>>> Handle(
        GetOperationalJobsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);

        var query = db.OperationalJobs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<OperationalJobStatus>(request.Status, true, out var status))
        {
            query = query.Where(j => j.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.JobType))
        {
            var type = request.JobType.Trim();
            query = query.Where(j => j.JobType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Queue))
        {
            var queueName = request.Queue.Trim();
            query = query.Where(j => j.Queue == queueName);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(j =>
                j.JobType.Contains(term)
                || (j.LastError != null && j.LastError.Contains(term))
                || (j.CorrelationId != null && j.CorrelationId.Contains(term))
                || (j.RelatedEntityId != null && j.RelatedEntityId.Contains(term)));
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(j => j.CreatedOnUtc >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(j => j.CreatedOnUtc <= request.DateTo.Value);
        }

        query = (request.Sort?.ToLowerInvariant()) switch
        {
            "created_asc" => query.OrderBy(j => j.CreatedOnUtc),
            "priority_desc" => query.OrderByDescending(j => j.Priority).ThenByDescending(j => j.CreatedOnUtc),
            "status" => query.OrderBy(j => j.Status).ThenByDescending(j => j.CreatedOnUtc),
            _ => query.OrderByDescending(j => j.CreatedOnUtc),
        };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(MapJob).ToList();

        return Result.Success(new PagedResult<OperationalJobDto>(items, page, pageSize, total));
    }

    internal static OperationalJobDto MapJob(OperationalJob j)
    {
        double? duration = j.StartedOnUtc is { } start && j.FinishedOnUtc is { } finish
            ? (finish - start).TotalSeconds
            : null;

        return new OperationalJobDto(
            j.Id,
            j.JobType,
            j.Status.ToString(),
            j.Priority.ToString(),
            j.Queue,
            j.ProgressPercent,
            j.Attempts,
            j.MaxAttempts,
            j.WorkerId,
            j.CorrelationId,
            j.RelatedEntityType,
            j.RelatedEntityId,
            j.LastError,
            j.CreatedOnUtc,
            j.StartedOnUtc,
            j.FinishedOnUtc,
            j.NextVisibleOnUtc,
            j.LastRetryOnUtc,
            duration);
    }
}

public sealed record GetOperationalJobByIdQuery(Guid Id) : IRequest<Result<OperationalJobDto>>;

internal sealed class GetOperationalJobByIdQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetOperationalJobByIdQuery, Result<OperationalJobDto>>
{
    public async Task<Result<OperationalJobDto>> Handle(
        GetOperationalJobByIdQuery request,
        CancellationToken cancellationToken)
    {
        var job = await db.OperationalJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);

        if (job is null)
        {
            return Result.Failure<OperationalJobDto>(new Error("Operations.JobNotFound", "Operational job was not found."));
        }

        return Result.Success(GetOperationalJobsQueryHandler.MapJob(job));
    }
}

public sealed record GetJobExecutionHistoryQuery(Guid JobId) : IRequest<Result<IReadOnlyList<BackgroundJobExecutionHistoryDto>>>;

internal sealed class GetJobExecutionHistoryQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetJobExecutionHistoryQuery, Result<IReadOnlyList<BackgroundJobExecutionHistoryDto>>>
{
    public async Task<Result<IReadOnlyList<BackgroundJobExecutionHistoryDto>>> Handle(
        GetJobExecutionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await db.BackgroundJobExecutionHistory.AsNoTracking()
            .Where(h => h.JobId == request.JobId)
            .OrderByDescending(h => h.AttemptNumber)
            .Select(h => new BackgroundJobExecutionHistoryDto(
                h.Id,
                h.AttemptNumber,
                h.Status.ToString(),
                h.StartedOnUtc,
                h.FinishedOnUtc,
                h.DurationMs,
                h.Exception,
                h.WorkerId,
                h.CorrelationId))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BackgroundJobExecutionHistoryDto>>(rows);
    }
}

public sealed record GetRecurringJobDefinitionsQuery : IRequest<Result<IReadOnlyList<RecurringJobDefinitionDto>>>;

internal sealed class GetRecurringJobDefinitionsQueryHandler(
    IRecurringJobRegistry registry,
    ICommerceDbContext db) : IRequestHandler<GetRecurringJobDefinitionsQuery, Result<IReadOnlyList<RecurringJobDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<RecurringJobDefinitionDto>>> Handle(
        GetRecurringJobDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var definitions = registry.GetAll();
        var jobTypes = definitions.Select(d => d.JobType).Distinct().ToArray();

        var lastEnqueued = await db.OperationalJobs.AsNoTracking()
            .Where(j => jobTypes.Contains(j.JobType))
            .GroupBy(j => j.JobType)
            .Select(g => new { JobType = g.Key, LastCreatedOnUtc = g.Max(j => j.CreatedOnUtc) })
            .ToDictionaryAsync(g => g.JobType, g => g.LastCreatedOnUtc, cancellationToken);

        var items = definitions
            .OrderBy(d => d.Key)
            .Select(d => new RecurringJobDefinitionDto(
                d.Key,
                d.JobType,
                d.Queue,
                d.Priority.ToString(),
                d.Interval.TotalMinutes,
                lastEnqueued.TryGetValue(d.JobType, out var last) ? last : null))
            .ToList();

        return Result.Success<IReadOnlyList<RecurringJobDefinitionDto>>(items);
    }
}

public sealed record RetryOperationalJobCommand(Guid Id) : IRequest<Result>;

internal sealed class RetryOperationalJobCommandHandler(
    IOperationalJobQueue queue,
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<RetryOperationalJobCommand, Result>
{
    public async Task<Result> Handle(RetryOperationalJobCommand request, CancellationToken cancellationToken)
    {
        var ok = await queue.RetryAsync(request.Id, cancellationToken);
        if (!ok)
        {
            return Result.Failure(new Error("Operations.JobNotFound", "Operational job was not found."));
        }

        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "JobRetried",
            currentUser.UserId,
            currentUser.UserId,
            "OperationalJob",
            request.Id.ToString()));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RetryAllFailedJobsCommand : IRequest<Result<int>>;

internal sealed class RetryAllFailedJobsCommandHandler(
    IOperationalJobQueue queue,
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<RetryAllFailedJobsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RetryAllFailedJobsCommand request, CancellationToken cancellationToken)
    {
        var count = await queue.RetryAllFailedAsync(cancellationToken);
        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "JobsRetryAllFailed",
            currentUser.UserId,
            currentUser.UserId,
            details: $"Retried {count} failed job(s)."));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(count);
    }
}

public sealed record CancelOperationalJobCommand(Guid Id) : IRequest<Result>;

internal sealed class CancelOperationalJobCommandHandler(
    IOperationalJobQueue queue,
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<CancelOperationalJobCommand, Result>
{
    public async Task<Result> Handle(CancelOperationalJobCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var ok = await queue.CancelAsync(request.Id, cancellationToken);
            if (!ok)
            {
                return Result.Failure(new Error("Operations.JobNotFound", "Operational job was not found."));
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new Error("Operations.CancelInvalid", ex.Message));
        }

        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "JobCancelled",
            currentUser.UserId,
            currentUser.UserId,
            "OperationalJob",
            request.Id.ToString()));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ClearCompletedJobsCommand : IRequest<Result<int>>;

internal sealed class ClearCompletedJobsCommandHandler(
    IOperationalJobQueue queue,
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<ClearCompletedJobsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ClearCompletedJobsCommand request, CancellationToken cancellationToken)
    {
        var count = await queue.ClearCompletedAsync(cancellationToken);
        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "JobsClearedCompleted",
            currentUser.UserId,
            currentUser.UserId,
            details: $"Cleared {count} completed job(s)."));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(count);
    }
}

public sealed record GetDeliveryMonitorQuery(
    string? Status,
    string? Search,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<DeliveryMonitorItemDto>>>;

internal sealed class GetDeliveryMonitorQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetDeliveryMonitorQuery, Result<PagedResult<DeliveryMonitorItemDto>>>
{
    public async Task<Result<PagedResult<DeliveryMonitorItemDto>>> Handle(
        GetDeliveryMonitorQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);

        var ordersQuery = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            ordersQuery = ordersQuery.Where(o =>
                o.OrderNumber.Contains(term) || o.Email.Contains(term));
        }

        var orders = await ordersQuery
            .OrderByDescending(o => o.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();
        var keys = await db.OrderLicenseKeys.AsNoTracking()
            .Where(k => orderIds.Contains(k.OrderId))
            .ToListAsync(cancellationToken);
        var keysByOrder = keys.GroupBy(k => k.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLicenseKey>)g.ToList());

        var items = orders.Select(o =>
        {
            var orderKeys = keysByOrder.TryGetValue(o.Id, out var k) ? k : Array.Empty<OrderLicenseKey>();
            var delivery = AdminOrderMapper.ResolveDeliveryStatus(o, orderKeys);
            return new DeliveryMonitorItemDto(
                o.Id,
                o.OrderNumber,
                o.Email,
                o.Status.ToString(),
                o.PaymentStatus.ToString(),
                delivery,
                o.Items.Sum(i => i.Quantity),
                orderKeys.Count,
                o.CreatedOnUtc,
                orderKeys.Count > 0 ? orderKeys.Max(x => x.CreatedOnUtc) : null);
        });

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(i => i.DeliveryStatus.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
        }

        var list = items.ToList();
        var total = list.Count;
        var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Result.Success(new PagedResult<DeliveryMonitorItemDto>(pageItems, page, pageSize, total));
    }
}

public sealed record RetryDeliveryCommand(Guid OrderId) : IRequest<Result<Guid>>;

internal sealed class RetryDeliveryCommandHandler(
    IOperationalJobQueue queue,
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<RetryDeliveryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RetryDeliveryCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Orders.AsNoTracking().AnyAsync(o => o.Id == request.OrderId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<Guid>(new Error("Operations.OrderNotFound", "Order was not found."));
        }

        var payload = JsonSerializer.Serialize(new { orderId = request.OrderId });
        var job = await queue.EnqueueAsync(
            OperationalJobTypes.RetryDelivery,
            payload,
            OperationalJobPriority.High,
            relatedEntityType: "Order",
            relatedEntityId: request.OrderId.ToString(),
            cancellationToken: cancellationToken);

        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "DeliveryRetryEnqueued",
            currentUser.UserId,
            currentUser.UserId,
            "Order",
            request.OrderId.ToString(),
            $"Job {job.Id}"));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(job.Id);
    }
}

public sealed record GetSupplierOperationsQuery : IRequest<Result<IReadOnlyList<SupplierOperationsItemDto>>>;

internal sealed class GetSupplierOperationsQueryHandler(ICatalogDbContext catalogDb)
    : IRequestHandler<GetSupplierOperationsQuery, Result<IReadOnlyList<SupplierOperationsItemDto>>>
{
    public async Task<Result<IReadOnlyList<SupplierOperationsItemDto>>> Handle(
        GetSupplierOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var suppliers = await catalogDb.InventorySuppliers.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.CompanyName)
            .ToListAsync(cancellationToken);

        var auditCounts = await catalogDb.InventoryAuditLogs.AsNoTracking()
            .Where(a => a.OccurredOnUtc >= since && a.SupplierId != null)
            .GroupBy(a => a.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = auditCounts.ToDictionary(a => a.SupplierId, a => a.Count);

        var items = suppliers.Select(s => new SupplierOperationsItemDto(
            s.Id,
            s.CompanyName,
            s.Status.ToString(),
            s.Country,
            s.Currency,
            s.Email,
            s.CreatedOnUtc,
            s.ModifiedOnUtc,
            countMap.TryGetValue(s.Id, out var c) ? c : 0)).ToList();

        return Result.Success<IReadOnlyList<SupplierOperationsItemDto>>(items);
    }
}

public sealed record GetApiRequestLogsQuery(
    string? Method,
    string? Path,
    string? UserId,
    int? MinStatusCode,
    int? MaxStatusCode,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<ApiRequestLogDto>>>;

internal sealed class GetApiRequestLogsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetApiRequestLogsQuery, Result<PagedResult<ApiRequestLogDto>>>
{
    public async Task<Result<PagedResult<ApiRequestLogDto>>> Handle(
        GetApiRequestLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, 200);
        var query = db.ApiRequestLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Method))
        {
            var method = request.Method.Trim().ToUpperInvariant();
            query = query.Where(l => l.Method == method);
        }

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            var path = request.Path.Trim();
            query = query.Where(l => l.Path.Contains(path));
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            var userId = request.UserId.Trim();
            query = query.Where(l => l.UserId != null && l.UserId.Contains(userId));
        }

        if (request.MinStatusCode.HasValue)
        {
            query = query.Where(l => l.StatusCode >= request.MinStatusCode.Value);
        }

        if (request.MaxStatusCode.HasValue)
        {
            query = query.Where(l => l.StatusCode <= request.MaxStatusCode.Value);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(l => l.TimestampUtc >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(l => l.TimestampUtc <= request.DateTo.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ApiRequestLogDto(
                l.Id,
                l.TimestampUtc,
                l.Method,
                l.Path,
                l.StatusCode,
                l.DurationMs,
                l.CorrelationId,
                l.UserId,
                l.IpAddress,
                l.RequestSizeBytes,
                l.ResponseSizeBytes,
                l.Exception))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ApiRequestLogDto>(items, page, pageSize, total));
    }
}

public sealed record GetSystemHealthQuery : IRequest<Result<SystemHealthDto>>;

internal sealed class GetSystemHealthQueryHandler(ISystemHealthService health)
    : IRequestHandler<GetSystemHealthQuery, Result<SystemHealthDto>>
{
    public async Task<Result<SystemHealthDto>> Handle(
        GetSystemHealthQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await health.GetHealthAsync(cancellationToken));
}

public sealed record GetOperationalAlertsQuery(
    bool? Acknowledged,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<OperationalAlertDto>>>;

internal sealed class GetOperationalAlertsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetOperationalAlertsQuery, Result<PagedResult<OperationalAlertDto>>>
{
    public async Task<Result<PagedResult<OperationalAlertDto>>> Handle(
        GetOperationalAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);
        var query = db.OperationalAlerts.AsNoTracking().AsQueryable();

        if (request.Acknowledged.HasValue)
        {
            query = query.Where(a => a.IsAcknowledged == request.Acknowledged.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new OperationalAlertDto(
                a.Id,
                a.Code,
                a.Title,
                a.Message,
                a.Severity.ToString(),
                a.RelatedEntityType,
                a.RelatedEntityId,
                a.CreatedOnUtc,
                a.IsAcknowledged,
                a.AcknowledgedOnUtc,
                a.AcknowledgedByUserId))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<OperationalAlertDto>(items, page, pageSize, total));
    }
}

public sealed record AcknowledgeOperationalAlertCommand(Guid Id) : IRequest<Result>;

internal sealed class AcknowledgeOperationalAlertCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<AcknowledgeOperationalAlertCommand, Result>
{
    public async Task<Result> Handle(AcknowledgeOperationalAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await db.OperationalAlerts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (alert is null)
        {
            return Result.Failure(new Error("Operations.AlertNotFound", "Operational alert was not found."));
        }

        alert.Acknowledge(currentUser.UserId);
        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "AlertAcknowledged",
            currentUser.UserId,
            currentUser.UserId,
            "OperationalAlert",
            request.Id.ToString()));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ExportOperationsQuery(string Type, string Format)
    : IRequest<Result<OperationsExportDto>>;

internal sealed class ExportOperationsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<ExportOperationsQuery, Result<OperationsExportDto>>
{
    public async Task<Result<OperationsExportDto>> Handle(
        ExportOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var format = (request.Format ?? "csv").Trim().ToLowerInvariant();
        if (format is "excel")
        {
            format = "csv";
        }

        var type = (request.Type ?? "jobs").Trim().ToLowerInvariant();

        return type switch
        {
            "jobs" => await ExportJobsAsync(format, cancellationToken),
            "api-logs" => await ExportApiLogsAsync(format, cancellationToken),
            "deliveries" => await ExportDeliveriesAsync(format, cancellationToken),
            _ => Result.Failure<OperationsExportDto>(
                new Error("Operations.ExportTypeInvalid", "Export type must be jobs, api-logs, or deliveries.")),
        };
    }

    private async Task<Result<OperationsExportDto>> ExportJobsAsync(string format, CancellationToken ct)
    {
        var jobs = await db.OperationalJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedOnUtc)
            .Take(5000)
            .ToListAsync(ct);

        if (format == "json")
        {
            var json = JsonSerializer.Serialize(jobs.Select(j => new
            {
                j.Id,
                j.JobType,
                Status = j.Status.ToString(),
                Priority = j.Priority.ToString(),
                j.Attempts,
                j.LastError,
                j.CreatedOnUtc,
                j.FinishedOnUtc,
            }));
            return Result.Success(new OperationsExportDto("json", "application/json", "operational-jobs.json", json));
        }

        var sb = new StringBuilder();
        sb.AppendLine("Id,JobType,Status,Priority,Attempts,LastError,CreatedOnUtc,FinishedOnUtc");
        foreach (var j in jobs)
        {
            sb.AppendLine(string.Join(',',
                j.Id,
                Csv(j.JobType),
                j.Status,
                j.Priority,
                j.Attempts,
                Csv(j.LastError),
                j.CreatedOnUtc.ToString("O", CultureInfo.InvariantCulture),
                j.FinishedOnUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return Result.Success(new OperationsExportDto(
            "csv",
            "text/csv",
            "operational-jobs.csv",
            sb.ToString()));
    }

    private async Task<Result<OperationsExportDto>> ExportApiLogsAsync(string format, CancellationToken ct)
    {
        var logs = await db.ApiRequestLogs.AsNoTracking()
            .OrderByDescending(l => l.TimestampUtc)
            .Take(5000)
            .ToListAsync(ct);

        if (format == "json")
        {
            var json = JsonSerializer.Serialize(logs.Select(l => new
            {
                l.Id,
                l.TimestampUtc,
                l.Method,
                l.Path,
                l.StatusCode,
                l.DurationMs,
            }));
            return Result.Success(new OperationsExportDto("json", "application/json", "api-request-logs.json", json));
        }

        var sb = new StringBuilder();
        sb.AppendLine("Id,TimestampUtc,Method,Path,StatusCode,DurationMs");
        foreach (var l in logs)
        {
            sb.AppendLine(string.Join(',',
                l.Id,
                l.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                Csv(l.Method),
                Csv(l.Path),
                l.StatusCode,
                l.DurationMs));
        }

        return Result.Success(new OperationsExportDto("csv", "text/csv", "api-request-logs.csv", sb.ToString()));
    }

    private async Task<Result<OperationsExportDto>> ExportDeliveriesAsync(string format, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Include(o => o.Items)
            .OrderByDescending(o => o.CreatedOnUtc)
            .Take(2000)
            .ToListAsync(ct);
        var keys = await db.OrderLicenseKeys.AsNoTracking().ToListAsync(ct);
        var keysByOrder = keys.GroupBy(k => k.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = orders.Select(o =>
        {
            keysByOrder.TryGetValue(o.Id, out var orderKeys);
            orderKeys ??= [];
            return new
            {
                o.Id,
                o.OrderNumber,
                o.Email,
                DeliveryStatus = AdminOrderMapper.ResolveDeliveryStatus(o, orderKeys),
                Keys = orderKeys.Count,
                o.CreatedOnUtc,
            };
        }).ToList();

        if (format == "json")
        {
            return Result.Success(new OperationsExportDto(
                "json",
                "application/json",
                "deliveries.json",
                JsonSerializer.Serialize(rows)));
        }

        var sb = new StringBuilder();
        sb.AppendLine("OrderId,OrderNumber,Email,DeliveryStatus,Keys,CreatedOnUtc");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.Id,
                Csv(r.OrderNumber),
                Csv(r.Email),
                Csv(r.DeliveryStatus),
                r.Keys,
                r.CreatedOnUtc.ToString("O", CultureInfo.InvariantCulture)));
        }

        return Result.Success(new OperationsExportDto("csv", "text/csv", "deliveries.csv", sb.ToString()));
    }

    private static string Csv(string? value)
    {
        var raw = value ?? string.Empty;
        if (raw.Contains('"') || raw.Contains(',') || raw.Contains('\n'))
        {
            return $"\"{raw.Replace("\"", "\"\"")}\"";
        }

        return raw;
    }
}
