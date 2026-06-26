using HAMBOX.Modules.Catalog.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HAMBOX.Modules.Catalog.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Prevents optimistic concurrency failures when only child entities of a product aggregate changed.
/// </summary>
internal sealed class ProductAggregateChangeInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CatalogChangeTracker.SuppressProductRootUpdatesWhenOnlyChildrenChanged(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CatalogChangeTracker.SuppressProductRootUpdatesWhenOnlyChildrenChanged(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
