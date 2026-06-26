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
        var connection = _commerceDbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        await _commerceDbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken);

        if (_catalogDbContext is CatalogDbContext catalogDbContext)
        {
            await catalogDbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken);
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
