using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Drafts;
using HAMBOX.Modules.Catalog.Domain.Images;
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
    /// Gets the product drafts database set.
    /// </summary>
    DbSet<ProductDraft> ProductDrafts { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation, containing the number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
