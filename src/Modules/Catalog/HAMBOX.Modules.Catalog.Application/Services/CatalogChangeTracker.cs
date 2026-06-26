using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Helpers for catalog EF Core change tracking.
/// </summary>
public static class CatalogChangeTracker
{
    private static readonly HashSet<string> IgnoredScalarProperties = new(StringComparer.Ordinal)
    {
        nameof(BaseEntity.ModifiedOnUtc),
        nameof(IAuditable.CreatedBy),
        nameof(IAuditable.ModifiedBy),
        "RowVersion",
    };

    /// <summary>
    /// Prevents optimistic concurrency failures when only child entities changed on an aggregate root.
    /// Must run during <see cref="DbContext.SaveChanges"/> after EF has detected changes.
    /// </summary>
    public static void SuppressProductRootUpdatesWhenOnlyChildrenChanged(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Product>())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var hasBusinessScalarChanges = entry.Properties
                .Where(property => !IgnoredScalarProperties.Contains(property.Metadata.Name))
                .Any(property =>
                    property.IsModified &&
                    !Equals(property.OriginalValue, property.CurrentValue));

            if (!hasBusinessScalarChanges)
            {
                entry.State = EntityState.Unchanged;
            }
        }
    }
}
