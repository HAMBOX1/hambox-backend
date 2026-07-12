using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Carts;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Reconciles EF change-tracker state with the in-memory shopping cart aggregate.
/// </summary>
internal static class CartPersistenceHelper
{
    /// <summary>
    /// Ensures cart line entities are in the correct state before <see cref="DbContext.SaveChangesAsync"/>.
    /// Field-backed <see cref="ShoppingCart.Items"/> collections can incorrectly mark existing lines
    /// as deleted when a new line is appended to a tracked cart.
    /// </summary>
    internal static async Task PrepareForSaveAsync(
        ICommerceDbContext commerceDbContext,
        ShoppingCart cart,
        CancellationToken cancellationToken)
    {
        var dbContext = (DbContext)commerceDbContext;

        var persistedItemIds = await commerceDbContext.CartItems
            .AsNoTracking()
            .Where(i => i.ShoppingCartId == cart.Id)
            .Select(i => i.Id)
            .ToHashSetAsync(cancellationToken);

        var inMemoryItemIds = cart.Items.Select(i => i.Id).ToHashSet();

        foreach (var entry in dbContext.ChangeTracker.Entries<CartItem>()
            .Where(e => e.Entity.ShoppingCartId == cart.Id)
            .ToList())
        {
            if (entry.State == EntityState.Deleted && inMemoryItemIds.Contains(entry.Entity.Id))
            {
                entry.State = EntityState.Modified;
            }
            else if (entry.State != EntityState.Deleted && !inMemoryItemIds.Contains(entry.Entity.Id))
            {
                entry.State = EntityState.Deleted;
            }
        }

        foreach (var item in cart.Items)
        {
            var entry = dbContext.Entry(item);

            if (entry.State == EntityState.Detached)
            {
                commerceDbContext.CartItems.Add(item);
                continue;
            }

            if (!persistedItemIds.Contains(item.Id) && entry.State != EntityState.Added)
            {
                entry.State = EntityState.Added;
            }
        }
    }
}
