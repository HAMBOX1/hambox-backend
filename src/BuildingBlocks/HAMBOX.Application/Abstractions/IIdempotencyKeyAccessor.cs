namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Reads the client-supplied idempotency key for the current request.
/// </summary>
public interface IIdempotencyKeyAccessor
{
    /// <summary>
    /// Gets the value of the <c>Idempotency-Key</c> header for the current request, if present.
    /// </summary>
    string? GetIdempotencyKey();
}
