using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;

internal sealed class GetCatalogImportLookupsQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetCatalogImportLookupsQuery, Result<CatalogImportLookups>>
{
    /// <summary>The four currencies HAMBOX displays storefront prices in (CLAUDE.md §1) — Price itself always persists in USD, so this is display/PurchaseCost metadata only, not a lookup table.</summary>
    private static readonly string[] Currencies = ["USD", "EUR", "EGP", "SAR"];

    public async Task<Result<CatalogImportLookups>> Handle(GetCatalogImportLookupsQuery request, CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking()
            .OrderBy(c => c.NameEn)
            .Select(c => new CatalogImportLookupItem(c.Slug, c.NameEn))
            .ToListAsync(cancellationToken);

        var collections = await dbContext.ProductCollections.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CatalogImportLookupItem(c.Name, c.Name))
            .ToListAsync(cancellationToken);

        // Distinct group keys already in use catalog-wide — generated fresh from the DB on every
        // template download ("no code changes needed" per the import assistant requirements), not
        // a hardcoded enum. Deduped/grouped in memory since a Key can recur with a differently-cased
        // DisplayName across unrelated products.
        var variantGroups = (await dbContext.ProductOptionGroups.AsNoTracking()
                .Select(g => new { g.Key, g.DisplayName })
                .ToListAsync(cancellationToken))
            .GroupBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CatalogImportLookupItem(g.Key, g.First().DisplayName))
            .OrderBy(g => g.Label)
            .ToList();

        var lookups = new CatalogImportLookups(
            categories,
            collections,
            variantGroups,
            Enum.GetNames<ProductStatus>(),
            Enum.GetNames<ProductVariantStatus>(),
            Currencies,
            Enum.GetNames<CatalogSkuStrategy>(),
            Enum.GetNames<CatalogDuplicateStrategy>());

        return Result.Success(lookups);
    }
}
