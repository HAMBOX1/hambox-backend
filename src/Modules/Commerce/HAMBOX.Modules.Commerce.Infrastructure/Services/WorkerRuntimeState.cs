using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class WorkerRuntimeState : IWorkerRuntimeState
{
    private long _processed;
    private long _succeeded;
    private long _failed;

    public WorkerRuntimeState()
    {
        WorkerId = $"ops-{Environment.MachineName}-{Guid.NewGuid():N}";
        if (WorkerId.Length > 128)
        {
            WorkerId = WorkerId[..128];
        }
    }

    public string WorkerId { get; }

    public bool IsRunning { get; private set; }

    public DateTimeOffset? LastHeartbeatUtc { get; private set; }

    public DateTimeOffset? LastTickUtc { get; private set; }

    public long ProcessedCount => Interlocked.Read(ref _processed);

    public long FailedCount => Interlocked.Read(ref _failed);

    public long SucceededCount => Interlocked.Read(ref _succeeded);

    public void MarkStarted() => IsRunning = true;

    public void MarkStopped() => IsRunning = false;

    public void Heartbeat() => LastHeartbeatUtc = DateTimeOffset.UtcNow;

    public void MarkTick() => LastTickUtc = DateTimeOffset.UtcNow;

    public void RecordSuccess()
    {
        Interlocked.Increment(ref _processed);
        Interlocked.Increment(ref _succeeded);
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _processed);
        Interlocked.Increment(ref _failed);
    }
}
