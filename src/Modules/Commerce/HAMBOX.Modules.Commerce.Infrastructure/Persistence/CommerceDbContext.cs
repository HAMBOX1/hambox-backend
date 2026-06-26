using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Persistence;

/// <summary>
/// Represents the Entity Framework Core database context for the Commerce module.
/// </summary>
public sealed class CommerceDbContext(DbContextOptions<CommerceDbContext> options)
    : DbContext(options), ICommerceDbContext
{
    /// <inheritdoc />
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();

    /// <inheritdoc />
    public DbSet<CartItem> CartItems => Set<CartItem>();

    /// <inheritdoc />
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc />
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <inheritdoc />
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    /// <inheritdoc />
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    /// <inheritdoc />
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    /// <inheritdoc />
    public DbSet<ReferralProfile> ReferralProfiles => Set<ReferralProfile>();

    /// <inheritdoc />
    public DbSet<ReferralHistoryEntry> ReferralHistoryEntries => Set<ReferralHistoryEntry>();

    /// <inheritdoc />
    public DbSet<OrderLicenseKey> OrderLicenseKeys => Set<OrderLicenseKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("commerce");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);
    }
}
