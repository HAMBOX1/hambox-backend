using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Analytics;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Collections;
using HAMBOX.Modules.Catalog.Domain.Drafts;
using HAMBOX.Modules.Catalog.Domain.Images;
using HAMBOX.Modules.Catalog.Domain.Instructions;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Lightweight InMemory-backed <see cref="ICatalogDbContext"/> double for variant lifecycle tests
/// (usage inspection, cleanup, archive, permanent delete). Deliberately NOT the real
/// <c>CatalogDbContext</c> (Infrastructure) — <see cref="HAMBOX.Modules.Catalog.Infrastructure.Services.InventoryEngine"/>'s
/// <c>DeleteVariantPermanentlyAsync</c>/<c>CleanupVariantAsync</c> only open a real DB transaction
/// and take a raw-SQL row lock when <c>_db is CatalogDbContext</c>; using this double instead makes
/// them skip straight to the plain check/mutate/save path, which is exactly what a fast unit test
/// wants — the SQL Server-specific transaction/locking mechanism itself is not exercised here and
/// would need a real SQL Server integration test (this repo has no such infrastructure for
/// InventoryEngine today, consistent with the rest of its test suite).
/// </summary>
public sealed class TestCatalogDbContext(DbContextOptions<TestCatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductCollection> ProductCollections => Set<ProductCollection>();
    public DbSet<ProductCollectionItem> ProductCollectionItems => Set<ProductCollectionItem>();
    public DbSet<ProductDraft> ProductDrafts => Set<ProductDraft>();
    public DbSet<ProductInstructions> ProductInstructions => Set<ProductInstructions>();
    public DbSet<ProductPlan> ProductPlans => Set<ProductPlan>();
    public DbSet<ProductOptionGroup> ProductOptionGroups => Set<ProductOptionGroup>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<OptionGroupTemplate> OptionGroupTemplates => Set<OptionGroupTemplate>();
    public DbSet<OptionGroupTemplateOption> OptionGroupTemplateOptions => Set<OptionGroupTemplateOption>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantOption> ProductVariantOptions => Set<ProductVariantOption>();
    public DbSet<InventorySupplier> InventorySuppliers => Set<InventorySupplier>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<DigitalInventoryCode> DigitalInventoryCodes => Set<DigitalInventoryCode>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryAuditLog> InventoryAuditLogs => Set<InventoryAuditLog>();
    public DbSet<SearchQueryLog> SearchQueryLogs => Set<SearchQueryLog>();
    public DbSet<ProductViewEvent> ProductViewEvents => Set<ProductViewEvent>();
    public DbSet<CatalogPackageJob> CatalogPackageJobs => Set<CatalogPackageJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>().HasKey(c => c.Id);
        modelBuilder.Entity<Product>().HasKey(p => p.Id);
        modelBuilder.Entity<ProductOptionGroup>().HasKey(g => g.Id);
        modelBuilder.Entity<ProductOptionGroup>().HasMany(g => g.Options).WithOne().HasForeignKey(o => o.OptionGroupId);
        modelBuilder.Entity<ProductOptionGroup>().Navigation(g => g.Options).HasField("_options");
        modelBuilder.Entity<ProductOption>().HasKey(o => o.Id);
        modelBuilder.Entity<OptionGroupTemplate>().HasKey(t => t.Id);
        modelBuilder.Entity<OptionGroupTemplate>().HasMany(t => t.Options).WithOne().HasForeignKey(o => o.TemplateId);
        modelBuilder.Entity<OptionGroupTemplate>().Navigation(t => t.Options).HasField("_options");
        modelBuilder.Entity<OptionGroupTemplateOption>().HasKey(o => o.Id);
        modelBuilder.Entity<ProductVariant>().HasKey(v => v.Id);
        // Real composite key (matches InventoryConfigurations) — EF's convention can't infer this.
        modelBuilder.Entity<ProductVariantOption>().HasKey(vo => new { vo.VariantId, vo.OptionId });
        modelBuilder.Entity<InventorySupplier>().HasKey(s => s.Id);
        modelBuilder.Entity<InventoryBatch>().HasKey(b => b.Id);
        modelBuilder.Entity<DigitalInventoryCode>().HasKey(c => c.Id);
        modelBuilder.Entity<InventoryReservation>().HasKey(r => r.Id);
        modelBuilder.Entity<InventoryAuditLog>().HasKey(a => a.Id);
    }
}
