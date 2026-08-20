using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.UnitTests.Commerce.Dot.TestDoubles;

internal sealed class FakeOperationalJobQueue : IOperationalJobQueue
{
    public List<string> EnqueuedJobTypes { get; } = [];

    public Task<OperationalJob> EnqueueAsync(
        string jobType,
        string? payloadJson = null,
        OperationalJobPriority priority = OperationalJobPriority.Normal,
        string queue = OperationalJobQueues.Default,
        string? correlationId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        int? maxAttempts = null,
        CancellationToken cancellationToken = default)
    {
        EnqueuedJobTypes.Add(jobType);
        return Task.FromResult(OperationalJob.Create(jobType, payloadJson, priority, queue, correlationId, relatedEntityType, relatedEntityId));
    }

    public Task<IReadOnlyList<OperationalJob>> ClaimNextBatchAsync(
        string workerId, int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OperationalJob>>([]);

    public Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<int> RetryAllFailedAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
