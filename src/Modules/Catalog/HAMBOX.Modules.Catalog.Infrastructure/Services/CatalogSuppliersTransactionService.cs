using System.Data;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Infrastructure.Services;

/// <summary>
/// Shares one raw ADO connection/transaction across <see cref="CatalogDbContext"/> and Suppliers'
/// <see cref="SuppliersDbContext"/> — the exact recipe Commerce's <c>CommerceTransactionService</c>
/// already uses for Commerce+Catalog, parameterized over a different pair of contexts. Needed only
/// when an import/export touches supplier mappings, since "never leave partial imports" requires
/// both sides to commit or roll back together.
/// </summary>
internal sealed class CatalogSuppliersTransactionService(
    CatalogDbContext catalogDbContext,
    ISuppliersDbContext suppliersDbContext) : ICatalogSuppliersTransactionService
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await catalogDbContext.Database.CloseConnectionAsync();
        if (suppliersDbContext is not SuppliersDbContext suppliersContext)
        {
            await action(cancellationToken);
            return;
        }

        await suppliersContext.Database.CloseConnectionAsync();

        var connection = catalogDbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        if (!ReferenceEquals(suppliersContext.Database.GetDbConnection(), connection))
        {
            suppliersContext.Database.SetDbConnection(connection);
        }

        await using var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        // See CommerceTransactionService for why automatic savepoints must be disabled when one raw
        // transaction is manually shared across two DbContext instances.
        catalogDbContext.Database.AutoSavepointsEnabled = false;
        await catalogDbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken);
        suppliersContext.Database.AutoSavepointsEnabled = false;
        await suppliersContext.Database.UseTransactionAsync(dbTransaction, cancellationToken);

        try
        {
            await action(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
