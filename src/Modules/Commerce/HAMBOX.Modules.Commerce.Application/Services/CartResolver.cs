using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Carts;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Resolves and creates shopping carts for users and guests.
/// </summary>
internal static class CartResolver
{
    public static async Task<(ShoppingCart Cart, string? GuestSessionId)> GetOrCreateCartAsync(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUser,
        string? guestSessionId,
        bool createGuestSessionIfMissing,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsAuthenticated && currentUser.UserId is not null)
        {
            var userCart = await dbContext.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId, cancellationToken);

            if (userCart is not null)
            {
                return (userCart, guestSessionId);
            }

            userCart = ShoppingCart.CreateForUser(currentUser.UserId);
            dbContext.ShoppingCarts.Add(userCart);
            return (userCart, guestSessionId);
        }

        if (string.IsNullOrWhiteSpace(guestSessionId))
        {
            if (!createGuestSessionIfMissing)
            {
                var emptyCart = ShoppingCart.CreateForGuest(Guid.NewGuid().ToString());
                return (emptyCart, null);
            }

            guestSessionId = Guid.NewGuid().ToString();
        }

        var guestCart = await dbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.GuestSessionId == guestSessionId, cancellationToken);

        if (guestCart is not null)
        {
            return (guestCart, guestSessionId);
        }

        guestCart = ShoppingCart.CreateForGuest(guestSessionId);
        dbContext.ShoppingCarts.Add(guestCart);
        return (guestCart, guestSessionId);
    }

    public static async Task<ShoppingCart?> FindCartAsync(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUser,
        string? guestSessionId,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsAuthenticated && currentUser.UserId is not null)
        {
            return await dbContext.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == currentUser.UserId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(guestSessionId))
        {
            return null;
        }

        return await dbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.GuestSessionId == guestSessionId, cancellationToken);
    }
}
