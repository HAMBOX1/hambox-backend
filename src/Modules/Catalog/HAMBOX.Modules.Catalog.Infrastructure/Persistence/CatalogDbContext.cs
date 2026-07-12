using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Analytics;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Drafts;
using HAMBOX.Modules.Catalog.Domain.Images;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Represents the Entity Framework Core database context for the Catalog module.
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    /// <summary>
    /// Gets or sets the categories table.
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>
    /// Gets or sets the products table.
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Gets or sets the product images table.
    /// </summary>
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    /// <summary>
    /// Gets or sets the product drafts table.
    /// </summary>
    public DbSet<ProductDraft> ProductDrafts => Set<ProductDraft>();

    public DbSet<ProductPlan> ProductPlans => Set<ProductPlan>();
    public DbSet<ProductOptionGroup> ProductOptionGroups => Set<ProductOptionGroup>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantOption> ProductVariantOptions => Set<ProductVariantOption>();
    public DbSet<InventorySupplier> InventorySuppliers => Set<InventorySupplier>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<DigitalInventoryCode> DigitalInventoryCodes => Set<DigitalInventoryCode>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryAuditLog> InventoryAuditLogs => Set<InventoryAuditLog>();
    public DbSet<SearchQueryLog> SearchQueryLogs => Set<SearchQueryLog>();
    public DbSet<ProductViewEvent> ProductViewEvents => Set<ProductViewEvent>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft-deletable entities.
    /// </summary>
    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var condition = System.Linq.Expressions.Expression.Equal(
                property,
                System.Linq.Expressions.Expression.Constant(false));
            var lambda = System.Linq.Expressions.Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
