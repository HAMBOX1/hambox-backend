using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Single source of truth for categorizing everything that references a ProductVariant. Used by
/// the read-only usage-inspection query, the cleanup command, and the permanent-delete command's
/// transactional re-check — one calculator, not three separate ad-hoc counts, so the three
/// operations can never silently disagree about what "in use" means.
/// </summary>
public static class VariantUsageCalculator
{
    public static async Task<VariantUsageDto> ComputeAsync(
        ICatalogDbContext db,
        ICommerceVariantUsageProvider commerceUsage,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var activeReservations = await db.InventoryReservations
            .AsNoTracking()
            .CountAsync(r => r.VariantId == variantId && r.IsActive, cancellationToken);

        var availableCodes = await db.DigitalInventoryCodes
            .AsNoTracking()
            .CountAsync(c => c.VariantId == variantId && c.Status == InventoryCodeStatus.Available, cancellationToken);

        var disabledCodes = await db.DigitalInventoryCodes
            .AsNoTracking()
            .CountAsync(c => c.VariantId == variantId && c.Status == InventoryCodeStatus.Disabled, cancellationToken);

        var soldCodes = await db.DigitalInventoryCodes
            .AsNoTracking()
            .CountAsync(c => c.VariantId == variantId && c.Status == InventoryCodeStatus.Sold, cancellationToken);

        // Reserved/Returned/Expired/Lost/Invalid: never sold, but each one already touched a real
        // checkout, refund, or fulfillment attempt — grouped rather than enumerated so a future
        // InventoryCodeStatus value defaults to protected instead of silently falling through.
        var otherProtectedCodes = await db.DigitalInventoryCodes
            .AsNoTracking()
            .CountAsync(
                c => c.VariantId == variantId
                    && c.Status != InventoryCodeStatus.Available
                    && c.Status != InventoryCodeStatus.Disabled
                    && c.Status != InventoryCodeStatus.Sold,
                cancellationToken);

        var inventoryBatches = await db.InventoryBatches
            .AsNoTracking()
            .CountAsync(b => b.VariantId == variantId, cancellationToken);

        var auditLogReferences = await db.InventoryAuditLogs
            .AsNoTracking()
            .CountAsync(a => a.VariantId == variantId, cancellationToken);

        var commerce = await commerceUsage.GetUsageAsync(variantId, cancellationToken);

        var safeToRemove = new VariantUsageCategoryDto(
            [
                new VariantUsageItemDto("ActiveReservations", activeReservations),
                new VariantUsageItemDto("AvailableInventoryCodes", availableCodes),
                new VariantUsageItemDto("DisabledInventoryCodes", disabledCodes),
                new VariantUsageItemDto("CartItems", commerce.CartItemCount),
            ],
            activeReservations + availableCodes + disabledCodes + commerce.CartItemCount);

        var safeToDetach = new VariantUsageCategoryDto(
            [
                new VariantUsageItemDto("InventoryBatches", inventoryBatches),
                new VariantUsageItemDto("InventoryAuditLogReferences", auditLogReferences),
            ],
            inventoryBatches + auditLogReferences);

        var protectedHistory = new VariantUsageCategoryDto(
            [
                new VariantUsageItemDto("SoldInventoryCodes", soldCodes),
                new VariantUsageItemDto("OtherProtectedInventoryCodes", otherProtectedCodes),
                new VariantUsageItemDto("OrderItems", commerce.OrderItemCount),
                new VariantUsageItemDto("OrderLicenseKeys", commerce.OrderLicenseKeyCount),
            ],
            soldCodes + otherProtectedCodes + commerce.OrderItemCount + commerce.OrderLicenseKeyCount);

        return new VariantUsageDto(
            variantId,
            safeToRemove,
            safeToDetach,
            protectedHistory,
            // Must mirror IInventoryEngine.DeleteVariantPermanentlyAsync's actual gate exactly —
            // both zero protected history AND zero un-cleaned-up removable data, never just one.
            CanPermanentlyDelete: protectedHistory.TotalCount == 0 && safeToRemove.TotalCount == 0);
    }
}
