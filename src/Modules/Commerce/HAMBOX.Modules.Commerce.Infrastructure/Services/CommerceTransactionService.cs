using System.Data;
using System.Data.Common;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Executes commerce and catalog operations within a shared database transaction.
/// </summary>
internal sealed class CommerceTransactionService : ICommerceTransactionService
{
    private readonly CommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;

    public CommerceTransactionService(
        CommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await _commerceDbContext.Database.CloseConnectionAsync();
        if (_catalogDbContext is CatalogDbContext catalogDbContext)
        {
            await catalogDbContext.Database.CloseConnectionAsync();
        }

        var connection = _commerceDbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        if (_catalogDbContext is CatalogDbContext enlistedCatalog
            && !ReferenceEquals(enlistedCatalog.Database.GetDbConnection(), connection))
        {
            enlistedCatalog.Database.SetDbConnection(connection);
        }

        await using var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        // EF's automatic savepoints assume each context is the sole owner of the ambient
        // transaction. With one raw transaction manually shared across two DbContext instances,
        // that bookkeeping breaks and a later SaveChanges call fails with "Cannot issue SAVE
        // TRANSACTION when there is no active transaction". Microsoft's documented fix for
        // sharing a transaction across contexts is to disable automatic savepoints on each.
        _commerceDbContext.Database.AutoSavepointsEnabled = false;
        await _commerceDbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken);
        if (_catalogDbContext is CatalogDbContext catalogWithSharedConnection)
        {
            catalogWithSharedConnection.Database.AutoSavepointsEnabled = false;
            await catalogWithSharedConnection.Database.UseTransactionAsync(dbTransaction, cancellationToken);
        }

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
