namespace HAMBOX.Modules.Commerce.Application.Contracts.Operations;

public sealed record OperationsDashboardDto(
    OperationsJobMetricsDto Jobs,
    OperationsOrderMetricsDto Orders,
    OperationsInventoryMetricsDto Inventory,
    OperationsSupplierMetricsDto Suppliers,
    OperationsApiMetricsDto Api,
    IReadOnlyList<OperationalAlertDto> RecentAlerts,
    WorkerRuntimeStateDto Worker,
    OperationsTimingMetricsDto Timings);

public sealed record OperationsJobMetricsDto(
    int Queued,
    int Running,
    int Completed,
    int Failed,
    int Retrying,
    int Cancelled,
    int Total);

public sealed record OperationsOrderMetricsDto(
    int WaitingDelivery,
    int Delivered,
    int Failed,
    int Processing);

public sealed record OperationsInventoryMetricsDto(
    int ActiveReservations,
    int ExpiredReservations,
    int LowStockVariants,
    int OutOfStockVariants);

public sealed record OperationsSupplierMetricsDto(
    int Active,
    int Inactive,
    int Total);

public sealed record OperationsApiMetricsDto(
    int ErrorsLastHour,
    int ErrorsLast24Hours,
    int RequestsLastHour,
    double AverageDurationMsLastHour);

public sealed record OperationsTimingMetricsDto(
    double? AverageJobProcessingSeconds,
    double? AverageDeliverySeconds);

public sealed record WorkerRuntimeStateDto(
    string WorkerId,
    bool IsRunning,
    DateTimeOffset? LastHeartbeatUtc,
    DateTimeOffset? LastTickUtc,
    long ProcessedCount,
    long SucceededCount,
    long FailedCount,
    int? HeartbeatAgeSeconds);

public sealed record OperationalJobDto(
    Guid Id,
    string JobType,
    string Status,
    string Priority,
    string Queue,
    int ProgressPercent,
    int Attempts,
    int MaxAttempts,
    string? WorkerId,
    string? CorrelationId,
    string? RelatedEntityType,
    string? RelatedEntityId,
    string? LastError,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? StartedOnUtc,
    DateTimeOffset? FinishedOnUtc,
    DateTimeOffset? NextVisibleOnUtc,
    DateTimeOffset? LastRetryOnUtc,
    double? DurationSeconds);

public sealed record BackgroundJobExecutionHistoryDto(
    Guid Id,
    int AttemptNumber,
    string Status,
    DateTimeOffset StartedOnUtc,
    DateTimeOffset? FinishedOnUtc,
    long? DurationMs,
    string? Exception,
    string? WorkerId,
    string? CorrelationId);

public sealed record RecurringJobDefinitionDto(
    string Key,
    string JobType,
    string Queue,
    string Priority,
    double IntervalMinutes,
    DateTimeOffset? LastEnqueuedOnUtc);

public sealed record DeliveryMonitorItemDto(
    Guid OrderId,
    string OrderNumber,
    string CustomerEmail,
    string OrderStatus,
    string PaymentStatus,
    string DeliveryStatus,
    int ItemsCount,
    int KeysDelivered,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? LastKeyDeliveredOnUtc);

public sealed record SupplierOperationsItemDto(
    Guid Id,
    string CompanyName,
    string Status,
    string? Country,
    string Currency,
    string? Email,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc,
    int RecentAuditCount);

public sealed record ApiRequestLogDto(
    Guid Id,
    DateTimeOffset TimestampUtc,
    string Method,
    string Path,
    int StatusCode,
    long DurationMs,
    string? CorrelationId,
    string? UserId,
    string? IpAddress,
    long? RequestSizeBytes,
    long? ResponseSizeBytes,
    string? Exception);

public sealed record OperationalAlertDto(
    Guid Id,
    string Code,
    string Title,
    string Message,
    string Severity,
    string? RelatedEntityType,
    string? RelatedEntityId,
    DateTimeOffset CreatedOnUtc,
    bool IsAcknowledged,
    DateTimeOffset? AcknowledgedOnUtc,
    string? AcknowledgedByUserId);

public sealed record SystemHealthComponentDto(
    string Name,
    string Status,
    string? Detail,
    long? LatencyMs);

public sealed record SystemHealthDto(
    string OverallStatus,
    DateTimeOffset CheckedOnUtc,
    IReadOnlyList<SystemHealthComponentDto> Components);

public sealed record OperationsExportDto(
    string Format,
    string ContentType,
    string FileName,
    string Content);
