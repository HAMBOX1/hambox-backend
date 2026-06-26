namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Executes operations within a shared database transaction across commerce and catalog contexts.
/// </summary>
public interface ICommerceTransactionService
{
    /// <summary>
    /// Executes the specified action within a database transaction.
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
