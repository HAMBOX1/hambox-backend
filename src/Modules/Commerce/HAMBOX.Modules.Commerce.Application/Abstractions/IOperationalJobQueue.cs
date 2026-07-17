using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface IOperationalJobQueue
{
    Task<OperationalJob> EnqueueAsync(
        string jobType,
        string? payloadJson = null,
        OperationalJobPriority priority = OperationalJobPriority.Normal,
        string queue = OperationalJobQueues.Default,
        string? correlationId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        int? maxAttempts = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationalJob>> ClaimNextBatchAsync(
        string workerId,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<int> RetryAllFailedAsync(CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default);
}
