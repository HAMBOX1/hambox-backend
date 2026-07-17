using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Idempotency;

public enum IdempotencyRecordState
{
    Processing = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>
/// Deduplicates a client request identified by an <c>Idempotency-Key</c> header, so the
/// protected command executes at most once and later requests replay its cached result.
/// </summary>
public sealed class IdempotencyKey : Entity
{
    private IdempotencyKey()
    {
    }

    private IdempotencyKey(
        Guid id,
        string key,
        string? userId,
        string endpoint,
        string requestHash,
        DateTimeOffset expiresOnUtc)
        : base(id)
    {
        Key = key;
        UserId = userId;
        Endpoint = endpoint;
        RequestHash = requestHash;
        State = IdempotencyRecordState.Processing;
        ExpiresOnUtc = expiresOnUtc;
    }

    public string Key { get; private set; } = null!;

    public string? UserId { get; private set; }

    public string Endpoint { get; private set; } = null!;

    public string RequestHash { get; private set; } = null!;

    public IdempotencyRecordState State { get; private set; }

    public int? ResponseStatusCode { get; private set; }

    public string? SerializedResponse { get; private set; }

    public DateTimeOffset? CompletedOnUtc { get; private set; }

    public DateTimeOffset ExpiresOnUtc { get; private set; }

    /// <summary>
    /// Concurrency token guarding against two concurrent requests both winning a reclaim
    /// (of a failed or stuck-processing record) for the same key.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public static IdempotencyKey Start(
        string key,
        string? userId,
        string endpoint,
        string requestHash,
        DateTimeOffset expiresOnUtc) =>
        new(Guid.NewGuid(), key, userId, endpoint, requestHash, expiresOnUtc);

    /// <summary>
    /// Whether this record's original attempt is old enough to be reclaimed by a new
    /// request under the same key (crash recovery / stuck-request safety valve).
    /// </summary>
    public bool IsStaleProcessing(TimeSpan processingTimeout) =>
        State == IdempotencyRecordState.Processing && CreatedOnUtc.Add(processingTimeout) < DateTimeOffset.UtcNow;

    /// <summary>
    /// Resets a failed or stuck-processing record so a new attempt can execute under it.
    /// </summary>
    public void Reclaim(string requestHash, DateTimeOffset expiresOnUtc)
    {
        RequestHash = requestHash;
        State = IdempotencyRecordState.Processing;
        ResponseStatusCode = null;
        SerializedResponse = null;
        CompletedOnUtc = null;
        ExpiresOnUtc = expiresOnUtc;
    }

    public void Complete(int statusCode, string? serializedResponse)
    {
        State = IdempotencyRecordState.Completed;
        ResponseStatusCode = statusCode;
        SerializedResponse = serializedResponse;
        CompletedOnUtc = DateTimeOffset.UtcNow;
    }

    public void Fail()
    {
        State = IdempotencyRecordState.Failed;
        CompletedOnUtc = DateTimeOffset.UtcNow;
    }
}
