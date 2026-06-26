using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Defines the database context contract for the Commerce module.
/// </summary>
public interface ICommerceDbContext
{
    /// <summary>
    /// Gets the shopping carts database set.
    /// </summary>
    DbSet<ShoppingCart> ShoppingCarts { get; }

    /// <summary>
    /// Gets the cart items database set.
    /// </summary>
    DbSet<CartItem> CartItems { get; }

    /// <summary>
    /// Gets the orders database set.
    /// </summary>
    DbSet<Order> Orders { get; }

    /// <summary>
    /// Gets the order items database set.
    /// </summary>
    DbSet<OrderItem> OrderItems { get; }

    /// <summary>
    /// Gets the wishlist items database set.
    /// </summary>
    DbSet<WishlistItem> WishlistItems { get; }

    /// <summary>
    /// Gets the product reviews database set.
    /// </summary>
    DbSet<ProductReview> ProductReviews { get; }

    /// <summary>
    /// Gets the user notifications database set.
    /// </summary>
    DbSet<UserNotification> UserNotifications { get; }

    /// <summary>
    /// Gets the referral profiles database set.
    /// </summary>
    DbSet<ReferralProfile> ReferralProfiles { get; }

    /// <summary>
    /// Gets the referral history entries database set.
    /// </summary>
    DbSet<ReferralHistoryEntry> ReferralHistoryEntries { get; }

    /// <summary>
    /// Gets the order license keys database set.
    /// </summary>
    DbSet<OrderLicenseKey> OrderLicenseKeys { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
