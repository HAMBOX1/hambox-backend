namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface IWorkerRuntimeState
{
    string WorkerId { get; }

    bool IsRunning { get; }

    DateTimeOffset? LastHeartbeatUtc { get; }

    DateTimeOffset? LastTickUtc { get; }

    long ProcessedCount { get; }

    long FailedCount { get; }

    long SucceededCount { get; }

    void MarkStarted();

    void MarkStopped();

    void Heartbeat();

    void MarkTick();

    void RecordSuccess();

    void RecordFailure();
}
