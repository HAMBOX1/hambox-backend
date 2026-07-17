namespace HAMBOX.Infrastructure.Options;

/// <summary>
/// Configuration for the idempotency infrastructure.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Idempotency";

    /// <summary>
    /// Gets or sets how long a completed idempotency record stays eligible for replay
    /// before it is considered expired.
    /// </summary>
    public int ExpirationHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets how long a record may sit in the "Processing" state before a new
    /// request with the same key is allowed to reclaim and retry it (e.g. after a crash
    /// mid-request). Concurrent requests within this window instead receive a conflict.
    /// </summary>
    public int ProcessingTimeoutMinutes { get; set; } = 2;
}
