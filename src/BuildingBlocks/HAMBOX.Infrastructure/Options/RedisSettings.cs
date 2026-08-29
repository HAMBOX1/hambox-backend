namespace HAMBOX.Infrastructure.Options;

/// <summary>
/// Configuration for the optional Redis-backed job-queue wake-up notifier
/// (<see cref="HAMBOX.Application.BackgroundJobs.IJobQueueNotifier"/>). Entirely optional: when
/// <see cref="ConnectionString"/> is empty, Redis is never contacted and the system falls back to
/// pure DB-polling, exactly as if Redis did not exist — see <c>RedisJobQueueNotifier</c>'s remarks for
/// why this is safe (Redis is a latency optimization, never a source of truth).
/// </summary>
public sealed class RedisSettings
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Gets or sets the Redis connection string (e.g. <c>localhost:6379</c>, or
    /// <c>host:port,password=...,ssl=true</c> for a secured/production endpoint). Never committed —
    /// supplied via environment variable/user-secrets/deployment configuration only. Empty/unset
    /// disables Redis entirely.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel-name prefix used for job wake-up pub/sub messages, so a shared Redis
    /// instance can be safely reused across environments (dev/staging/prod) without cross-talk.
    /// </summary>
    public string ChannelPrefix { get; set; } = "hambox:jobs:wakeup:";
}
