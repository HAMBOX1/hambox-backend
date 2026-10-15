namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Pushes a new critical operational alert to the people who must act on it (audit R44). The alert row in the
/// database is the record; this is the "somebody is told" part. Implementations must never throw.
/// </summary>
public interface IOperationalAlertNotifier
{
    Task NotifyAsync(string code, string title, string message, string severity, CancellationToken cancellationToken = default);
}
