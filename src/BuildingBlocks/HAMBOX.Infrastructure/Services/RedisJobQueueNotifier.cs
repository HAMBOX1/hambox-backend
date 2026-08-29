using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// Redis pub/sub-backed <see cref="IJobQueueNotifier"/> — registered only when
/// <see cref="RedisSettings.ConnectionString"/> is configured (see <c>AddSharedInfrastructure</c>).
/// This class is the ONLY place in the codebase that touches <c>StackExchange.Redis</c> for job
/// wake-ups; every business/domain caller goes through <see cref="IJobQueueNotifier"/> only.
/// </summary>
/// <remarks>
/// Every public member is wrapped so a Redis outage — connection refused, timeout, auth failure,
/// mid-flight disconnect — degrades to a silent no-op (logged at Warning) rather than throwing.
/// <see cref="IConnectionMultiplexer"/> already reconnects automatically on its own once the network
/// path recovers (StackExchange.Redis's built-in behavior); this class does not need its own retry
/// loop on top of that — the durable DB-polling worker is the real recovery path regardless.
/// </remarks>
internal sealed class RedisJobQueueNotifier(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisSettings> settings,
    ILogger<RedisJobQueueNotifier> logger) : IJobQueueNotifier
{
    public async Task NotifyAsync(string queue, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriber = connectionMultiplexer.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(ChannelFor(queue)), queue);
        }
        catch (Exception ex)
        {
            // The SQL job row is already durably committed by the time a caller reaches this point
            // (see OperationalJobQueue) — a failed publish only costs a bit of latency, never the job.
            logger.LogWarning(ex, "Redis publish failed for job wake-up on queue '{Queue}' — worker will pick it up on its next scheduled poll instead.", queue);
        }
    }

    public IAsyncDisposable Subscribe(string queue, Func<Task> onNotified)
    {
        ISubscriber subscriber;
        RedisChannel channel;
        Action<RedisChannel, RedisValue> handler = (_, _) => _ = InvokeSafelyAsync(onNotified);

        try
        {
            subscriber = connectionMultiplexer.GetSubscriber();
            channel = RedisChannel.Literal(ChannelFor(queue));
            subscriber.Subscribe(channel, handler);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis subscribe failed for queue '{Queue}' — falling back to polling only for this cycle.", queue);
            return EmptyAsyncDisposable.Instance;
        }

        return new Subscription(subscriber, channel, handler, logger);
    }

    private async Task InvokeSafelyAsync(Func<Task> onNotified)
    {
        try
        {
            await onNotified();
        }
        catch (Exception ex)
        {
            // A wake-up callback failing is never worse than a missed notification — the durable
            // polling loop still runs regardless.
            logger.LogWarning(ex, "Job wake-up callback threw — ignored.");
        }
    }

    private string ChannelFor(string queue) => settings.Value.ChannelPrefix + queue;

    private sealed class Subscription(
        ISubscriber subscriber, RedisChannel channel, Action<RedisChannel, RedisValue> handler, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await subscriber.UnsubscribeAsync(channel, handler);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis unsubscribe failed — harmless (the subscription's connection is being torn down regardless).");
            }
        }
    }
}
