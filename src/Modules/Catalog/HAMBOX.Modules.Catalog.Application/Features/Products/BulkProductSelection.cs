using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products;

/// <summary>
/// The selection carried by every bulk product endpoint: either an explicit set of ids, or a
/// filter to resolve server-side (for "select all matching filter", which can span far beyond
/// whatever page the admin has loaded).
/// </summary>
public sealed record BulkProductSelection(
    IReadOnlyList<Guid>? ProductIds,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    bool SelectAllMatching);

/// <summary>
/// Resolves a <see cref="BulkProductSelection"/> to a concrete id list, shared by every bulk
/// products handler so the filter semantics stay identical to <c>GetProductsQueryHandler</c>.
/// </summary>
internal static class BulkProductSelectionResolver
{
    private const int MaxMatches = 2000;

    public static async Task<Result<IReadOnlyList<Guid>>> ResolveAsync(
        ICatalogDbContext db,
        BulkProductSelection selection,
        CancellationToken cancellationToken)
    {
        if (selection.ProductIds is { Count: > 0 })
        {
            return Result.Success<IReadOnlyList<Guid>>(selection.ProductIds.Distinct().ToList());
        }

        if (!selection.SelectAllMatching)
        {
            return Result.Failure<IReadOnlyList<Guid>>(CatalogErrors.ProductBulkEmpty);
        }

        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(selection.SearchTerm))
        {
            query = query.Where(p => p.NameAr.Contains(selection.SearchTerm) ||
                                     p.NameEn.Contains(selection.SearchTerm) ||
                                     p.DescriptionAr.Contains(selection.SearchTerm) ||
                                     p.DescriptionEn.Contains(selection.SearchTerm));
        }

        if (selection.Status.HasValue)
        {
            query = query.Where(p => p.Status == selection.Status.Value);
        }

        if (selection.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == selection.CategoryId.Value);
        }

        var ids = await query.Select(p => p.Id).Take(MaxMatches + 1).ToListAsync(cancellationToken);

        if (ids.Count > MaxMatches)
        {
            return Result.Failure<IReadOnlyList<Guid>>(CatalogErrors.ProductBulkSelectionTooLarge(MaxMatches));
        }

        return Result.Success<IReadOnlyList<Guid>>(ids);
    }
}
