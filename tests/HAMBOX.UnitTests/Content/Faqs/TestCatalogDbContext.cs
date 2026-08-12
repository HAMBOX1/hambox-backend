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

namespace HAMBOX.UnitTests.Content.Faqs;

/// <summary>
/// Minimal InMemory-backed <see cref="ICatalogDbContext"/> double for testing Content module handlers
/// that validate a Faq's Product/Category <c>TargetId</c> against the real Catalog module (cross-module
/// read, see CreateFaqCommandHandler). Only <see cref="Products"/>/<see cref="Categories"/> are ever
/// queried by Faq handlers — the remaining DbSets exist solely to satisfy the interface contract.
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

        // Faq tests only ever read Category/Product by Id — a bare key mapping is enough, and avoids
        // pulling in the real CatalogDbContext's encryption converters/precision config (Infrastructure
        // layer this test project deliberately doesn't reference).
        modelBuilder.Entity<Category>().HasKey(c => c.Id);
        modelBuilder.Entity<Product>().HasKey(p => p.Id);
    }
}
