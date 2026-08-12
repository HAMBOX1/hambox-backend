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

namespace HAMBOX.Modules.Catalog.Application.Abstractions;

/// <summary>
/// Defines the database context contract for the Catalog module.
/// </summary>
public interface ICatalogDbContext
{
    /// <summary>
    /// Gets the categories database set.
    /// </summary>
    DbSet<Category> Categories { get; }

    /// <summary>
    /// Gets the products database set.
    /// </summary>
    DbSet<Product> Products { get; }

    /// <summary>
    /// Gets the product images database set.
    /// </summary>
    DbSet<ProductImage> ProductImages { get; }

    /// <summary>
    /// Gets the product additional-category assignments database set.
    /// </summary>
    DbSet<ProductCategory> ProductCategories { get; }

    /// <summary>
    /// Gets the collections database set (internal, owner-only product organization).
    /// </summary>
    DbSet<ProductCollection> ProductCollections { get; }

    /// <summary>
    /// Gets the product-collection assignments database set.
    /// </summary>
    DbSet<ProductCollectionItem> ProductCollectionItems { get; }

    /// <summary>
    /// Gets the product drafts database set.
    /// </summary>
    DbSet<ProductDraft> ProductDrafts { get; }

    /// <summary>
    /// Gets the product instructions database set.
    /// </summary>
    DbSet<ProductInstructions> ProductInstructions { get; }

    DbSet<ProductPlan> ProductPlans { get; }
    DbSet<ProductOptionGroup> ProductOptionGroups { get; }
    DbSet<ProductOption> ProductOptions { get; }
    DbSet<OptionGroupTemplate> OptionGroupTemplates { get; }
    DbSet<OptionGroupTemplateOption> OptionGroupTemplateOptions { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductVariantOption> ProductVariantOptions { get; }
    DbSet<InventorySupplier> InventorySuppliers { get; }
    DbSet<InventoryBatch> InventoryBatches { get; }
    DbSet<DigitalInventoryCode> DigitalInventoryCodes { get; }
    DbSet<InventoryReservation> InventoryReservations { get; }
    DbSet<InventoryAuditLog> InventoryAuditLogs { get; }
    DbSet<SearchQueryLog> SearchQueryLogs { get; }
    DbSet<ProductViewEvent> ProductViewEvents { get; }
    DbSet<CatalogPackageJob> CatalogPackageJobs { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation, containing the number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
