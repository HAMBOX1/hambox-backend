using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Minimal in-memory stand-in for <see cref="IWorkerRuntimeState"/>. Defaults to a fresh
/// heartbeat so tests that don't care about worker-staleness don't trip the WORKER_STALE alert.</summary>
internal sealed class FakeWorkerRuntimeState : IWorkerRuntimeState
{
    public string WorkerId { get; set; } = "test-worker";
    public bool IsRunning { get; set; } = true;
    public DateTimeOffset? LastHeartbeatUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTickUtc { get; set; } = DateTimeOffset.UtcNow;
    public long ProcessedCount { get; set; }
    public long FailedCount { get; set; }
    public long SucceededCount { get; set; }

    public void MarkStarted() => IsRunning = true;
    public void MarkStopped() => IsRunning = false;
    public void Heartbeat() => LastHeartbeatUtc = DateTimeOffset.UtcNow;
    public void MarkTick() => LastTickUtc = DateTimeOffset.UtcNow;
    public void RecordSuccess() => SucceededCount++;
    public void RecordFailure() => FailedCount++;
}
