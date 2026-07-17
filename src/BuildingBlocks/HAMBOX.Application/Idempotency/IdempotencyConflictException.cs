namespace HAMBOX.Application.Idempotency;

/// <summary>
/// Thrown when a request is received while another request with the same idempotency key
/// is still being processed. Mapped to <c>409 Conflict</c> by the global exception handler.
/// </summary>
public sealed class IdempotencyConflictException(string key)
    : Exception($"A request with idempotency key '{key}' is still being processed.")
{
    /// <summary>
    /// Gets the idempotency key that caused the conflict.
    /// </summary>
    public string Key { get; } = key;
}
