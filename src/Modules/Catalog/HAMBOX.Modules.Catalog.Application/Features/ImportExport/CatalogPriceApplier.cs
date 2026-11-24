using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

public sealed record CatalogPriceApplyResult(int Updated, int Skipped, IReadOnlyList<string> Errors);

/// <summary>
/// Sets each listed variant's cost, normal, Elite and sale prices from a Pricing sheet. Sheet amounts are in the row's currency
/// and stored in USD (<c>rates</c> are units of a currency per one USD). A blank cell leaves that price as it is; a sale is
/// changed only by a row that carries a sale price.
/// </summary>
public static class CatalogPriceApplier
{
    public static bool NeedsRates(IEnumerable<CatalogImportPlanRow<ParsedPriceRow>> prices) =>
        prices.Any(p => p.Status != CatalogImportRowStatus.Duplicate && !p.Row.Currency.Equals("USD", StringComparison.OrdinalIgnoreCase));

    public static async Task<CatalogPriceApplyResult> ApplyAsync(
        ICatalogDbContext db,
        IReadOnlyList<CatalogImportPlanRow<ParsedPriceRow>> prices,
        IReadOnlyDictionary<string, decimal> rates,
        CancellationToken cancellationToken)
    {
        var skipped = prices.Count(p => p.Status == CatalogImportRowStatus.Duplicate);
        var applicable = prices.Where(p => p.Status != CatalogImportRowStatus.Duplicate).ToList();
        var errors = new List<string>();
        var updated = 0;
        if (applicable.Count == 0)
        {
            return new CatalogPriceApplyResult(0, skipped, errors);
        }

        var skus = applicable.Select(p => p.Row.Sku.ToUpperInvariant()).ToList();
        var variants = (await db.ProductVariants.Where(v => skus.Contains(v.Sku)).ToListAsync(cancellationToken))
            .ToDictionary(v => v.Sku, StringComparer.OrdinalIgnoreCase);
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var productPrices = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Price, cancellationToken);

        foreach (var planRow in applicable)
        {
            var row = planRow.Row;
            if (planRow.Status == CatalogImportRowStatus.Invalid || !variants.TryGetValue(row.Sku, out var variant))
            {
                errors.Add($"Price for '{row.Sku}': {string.Join(' ', planRow.Errors)}");
                continue;
            }

            var rate = row.Currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? 1m : rates.GetValueOrDefault(row.Currency);
            if (rate <= 0)
            {
                errors.Add($"Price for '{row.Sku}': there is no exchange rate for {row.Currency}.");
                continue;
            }

            decimal? Usd(decimal? amount) => amount is { } value ? decimal.Round(value / rate, 2) : null;

            var normal = Usd(row.NormalPrice) ?? variant.PriceOverride ?? productPrices.GetValueOrDefault(variant.ProductId);
            var sale = Usd(row.SalePrice);
            if (sale is { } salePrice && salePrice >= normal)
            {
                errors.Add($"Price for '{row.Sku}': the sale price must be lower than the normal price.");
                continue;
            }

            if (row.CostPrice is not null)
            {
                variant.SetCostPrice(Usd(row.CostPrice));
            }

            if (row.NormalPrice is not null)
            {
                variant.SetPriceOverride(Usd(row.NormalPrice));
            }

            if (row.ElitePrice is not null)
            {
                variant.SetMemberPrice(Usd(row.ElitePrice));
            }

            if (sale is not null)
            {
                variant.SetSale(sale, row.SaleStart, row.SaleEnd);
            }

            updated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new CatalogPriceApplyResult(updated, skipped, errors);
    }
}
