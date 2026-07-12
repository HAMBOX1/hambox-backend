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

        await _commerceDbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken);
        if (_catalogDbContext is CatalogDbContext catalogWithSharedConnection)
        {
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
