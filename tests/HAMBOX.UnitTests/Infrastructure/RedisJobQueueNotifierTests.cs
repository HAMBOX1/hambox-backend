using HAMBOX.Infrastructure.Options;
using HAMBOX.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Proves <see cref="RedisJobQueueNotifier"/>'s core contract — "never throws, degrades to a no-op
/// on any Redis failure" — against a REAL <see cref="StackExchange.Redis.ConnectionMultiplexer"/>
/// pointed at an address nothing listens on, rather than a hand-rolled fake of the (very large)
/// <c>ISubscriber</c>/<c>IConnectionMultiplexer</c> interfaces. This exercises the actual
/// StackExchange.Redis client code path a real outage would hit, not just this class's own try/catch.
/// <c>AbortOnConnectFail = false</c> is the same setting <c>AddSharedInfrastructure</c> uses in
/// production, so <see cref="ConnectionMultiplexer.Connect(ConfigurationOptions, System.IO.TextWriter?)"/>
/// itself never throws even though nothing is reachable — exactly the "don't crash app startup on a
/// down Redis" behavior this test is really about.
/// </summary>
public sealed class RedisJobQueueNotifierTests : IAsyncLifetime
{
    private ConnectionMultiplexer _unreachable = null!;

    public Task InitializeAsync()
    {
        var options = new ConfigurationOptions
        {
            EndPoints = { "127.0.0.1:1" }, // nothing listens here
            AbortOnConnectFail = false,
            ConnectTimeout = 300,
            ConnectRetry = 0,
        };
        _unreachable = ConnectionMultiplexer.Connect(options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _unreachable.DisposeAsync();

    private RedisJobQueueNotifier CreateNotifier() =>
        new(_unreachable, Options.Create(new RedisSettings()), NullLogger<RedisJobQueueNotifier>.Instance);

    [Fact]
    public async Task NotifyAsync_RedisUnreachable_NeverThrows()
    {
        var notifier = CreateNotifier();

        // The real assertion is simply that this doesn't throw — a durably-saved OperationalJob row
        // (already committed by the caller, OperationalJobQueue.EnqueueAsync) must never be undone or
        // fail the request just because the wake-up publish couldn't reach Redis.
        await notifier.NotifyAsync("default");
    }

    [Fact]
    public async Task Subscribe_RedisUnreachable_NeverThrows_AndReturnsADisposableSubscription()
    {
        var notifier = CreateNotifier();

        var subscription = notifier.Subscribe("default", () => Task.CompletedTask);

        Assert.NotNull(subscription);
        await subscription.DisposeAsync(); // must also never throw
    }
}
