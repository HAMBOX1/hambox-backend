namespace HAMBOX.Application.Idempotency;

/// <summary>
/// Coordinates request deduplication for idempotent commands, backed by a persistent store.
/// </summary>
/// <remarks>
/// Implementations must guarantee that <see cref="BeginAsync"/> is thread-safe and race-safe
/// across concurrent requests (and, in a scaled-out deployment, across processes) — two
/// simultaneous callers with the same key must never both receive <see cref="IdempotencyState.Started"/>.
/// </remarks>
public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to begin processing a request under the given idempotency key.
    /// </summary>
    /// <param name="key">The client-supplied idempotency key.</param>
    /// <param name="userId">The current user identifier, if authenticated.</param>
    /// <param name="endpoint">A stable identifier for the operation being protected (e.g. the command name).</param>
    /// <param name="requestHash">A deterministic hash of the request payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IdempotencyOutcome> BeginAsync(
        string key,
        string? userId,
        string endpoint,
        string requestHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the successful completion of a request, persisting its response for replay.
    /// </summary>
    Task CompleteAsync(
        string key,
        int responseStatusCode,
        string? serializedResponse,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a request failed, allowing the key to be reclaimed by a subsequent retry.
    /// </summary>
    Task FailAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// The result of attempting to begin (or replay) an idempotent operation.
/// </summary>
public enum IdempotencyState
{
    /// <summary>A new attempt was started (or a stale/failed one reclaimed) — the handler should execute.</summary>
    Started,

    /// <summary>A completed response already exists for this key and should be replayed as-is.</summary>
    Replayed,

    /// <summary>Another request is currently executing under this key.</summary>
    Conflict,

    /// <summary>The key was reused with a different request payload or against a different operation.</summary>
    PayloadMismatch,
}

/// <summary>
/// The outcome of <see cref="IIdempotencyService.BeginAsync"/>.
/// </summary>
public sealed record IdempotencyOutcome(
    IdempotencyState State,
    int? StoredResponseStatusCode = null,
    string? StoredResponse = null)
{
    public static IdempotencyOutcome Started() => new(IdempotencyState.Started);

    public static IdempotencyOutcome Replayed(int statusCode, string? response) =>
        new(IdempotencyState.Replayed, statusCode, response);

    public static IdempotencyOutcome Conflict() => new(IdempotencyState.Conflict);

    public static IdempotencyOutcome PayloadMismatch() => new(IdempotencyState.PayloadMismatch);
}
