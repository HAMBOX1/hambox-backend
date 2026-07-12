namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Validates that a JWT session claim references an active user session.
/// </summary>
public interface ISessionValidator
{
    Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
