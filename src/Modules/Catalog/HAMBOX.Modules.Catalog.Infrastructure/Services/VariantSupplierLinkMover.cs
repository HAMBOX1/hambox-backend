using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Infrastructure.Services;

internal sealed class VariantSupplierLinkMover(ISuppliersDbContext suppliersDb) : IVariantSupplierLinkMover
{
    public async Task MoveAsync(IReadOnlyCollection<VariantMove> moves, CancellationToken cancellationToken)
    {
        if (moves.Count == 0)
        {
            return;
        }

        var fromProductIds = moves.Select(m => m.FromProductId).Distinct().ToList();
        var variantIds = moves.Select(m => m.VariantId).ToList();

        // Product-wide mappings (no variant) applied to every variant of the old product that had no
        // mapping of its own for that supplier. Once the variant lives under another product they would no
        // longer reach it, so each moved variant gets its own copy of the ones it relied on.
        var productWide = await suppliersDb.SupplierProductMappings
            .Where(m => m.InternalProductVariantId == null && fromProductIds.Contains(m.InternalProductId))
            .ToListAsync(cancellationToken);

        var variantSpecificSuppliers = await suppliersDb.SupplierProductMappings
            .Where(m => m.InternalProductVariantId != null && variantIds.Contains(m.InternalProductVariantId.Value))
            .Select(m => new { VariantId = m.InternalProductVariantId!.Value, m.SupplierId })
            .ToListAsync(cancellationToken);
        var covered = variantSpecificSuppliers.Select(x => (x.VariantId, x.SupplierId)).ToHashSet();

        foreach (var move in moves)
        {
            foreach (var mapping in productWide.Where(m => m.InternalProductId == move.FromProductId))
            {
                if (covered.Contains((move.VariantId, mapping.SupplierId)))
                {
                    continue;
                }

                suppliersDb.SupplierProductMappings.Add(SupplierProductMapping.Create(
                    mapping.SupplierId,
                    move.ToProductId,
                    mapping.ExternalProductId,
                    mapping.ExternalSku,
                    mapping.ExternalName,
                    mapping.BuyingPrice,
                    mapping.Currency,
                    mapping.Priority,
                    internalProductVariantId: move.VariantId,
                    marginPercentOverride: mapping.MarginPercentOverride));
            }
        }

        // The originals now point at a product that no longer exists in the catalog; keep them for history
        // but stop them taking part in routing.
        foreach (var mapping in productWide)
        {
            mapping.Update(
                mapping.ExternalProductId,
                mapping.ExternalSku,
                mapping.ExternalName,
                mapping.BuyingPrice,
                mapping.Currency,
                mapping.Priority,
                SupplierMappingStatus.Inactive,
                mapping.MarginPercentOverride);
        }

        await suppliersDb.SaveChangesAsync(cancellationToken);

        foreach (var move in moves)
        {
            await suppliersDb.SupplierProductMappings
                .Where(m => m.InternalProductVariantId == move.VariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.InternalProductId, move.ToProductId), cancellationToken);

            await suppliersDb.SupplierDerivedPrices
                .Where(p => p.InternalProductVariantId == move.VariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.InternalProductId, move.ToProductId), cancellationToken);
        }
    }
}
