using HAMBOX.Application.BackgroundJobs;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// The default <see cref="IJobQueueNotifier"/> when Redis is not configured (<see cref="Options.RedisSettings.ConnectionString"/>
/// is empty). A pure no-op: <see cref="Subscribe"/> returns a subscription that never invokes its
/// callback, so <c>OperationalJobWorker</c> transparently falls back to its own timed polling interval
/// — the system's baseline, fully-correct behavior with zero Redis involvement.
/// </summary>
internal sealed class NullJobQueueNotifier : IJobQueueNotifier
{
    public Task NotifyAsync(string queue, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IAsyncDisposable Subscribe(string queue, Func<Task> onNotified) => EmptyAsyncDisposable.Instance;
}
