using HAMBOX.Infrastructure.Services;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// The "Redis not configured" default — proves the system behaves as a pure no-op, which is what
/// makes DB-polling the correct, fully-functional baseline with zero Redis present at all.
/// </summary>
public sealed class NullJobQueueNotifierTests
{
    [Fact]
    public async Task NotifyAsync_CompletesWithoutError()
    {
        var notifier = new NullJobQueueNotifier();

        await notifier.NotifyAsync("default");
    }

    [Fact]
    public async Task Subscribe_NeverInvokesTheCallback()
    {
        var notifier = new NullJobQueueNotifier();
        var invoked = false;

        var subscription = notifier.Subscribe("default", () =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await Task.Delay(50); // give a real bug a chance to fire the callback spuriously
        Assert.False(invoked);

        await subscription.DisposeAsync();
    }
}
