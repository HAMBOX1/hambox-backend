using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;

internal sealed class MergeProductsCommandHandler : IRequestHandler<MergeProductsCommand, Result<MergeProductsResultDto>>
{
    private const string VariantOptionGroupKey = "variant";

    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MergeProductsCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<MergeProductsResultDto>> Handle(MergeProductsCommand request, CancellationToken cancellationToken)
    {
        var target = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.TargetProductId, cancellationToken);
        if (target is null)
        {
            return Result.Failure<MergeProductsResultDto>(CatalogErrors.ProductNotFound);
        }

        var sources = await _db.Products
            .Where(p => request.SourceProductIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (sources.Count != request.SourceProductIds.Distinct().Count())
        {
            return Result.Failure<MergeProductsResultDto>(CatalogErrors.ProductNotFound);
        }

        // A source that already has its own variants would be silently orphaned (its variants stay
        // put, but the product they belong to disappears from the main catalog list) — block this
        // rather than guess what the admin wants; they can handle that product on its own first.
        var sourceIdsWithVariants = await _db.ProductVariants
            .Where(v => !v.IsDeleted && request.SourceProductIds.Contains(v.ProductId))
            .Select(v => v.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sourceIdsWithVariants.Count > 0)
        {
            return Result.Failure<MergeProductsResultDto>(
                CatalogErrors.ProductMergeSourceHasVariants(sourceIdsWithVariants[0]));
        }

        // Product.StockQuantity is a bare counter with no serials/codes behind it — a variant's
        // stock is entirely code-driven (DigitalInventoryCode), so this count has no migration
        // path and would silently disappear. Require an explicit confirmation before doing that.
        var sourceIdsWithStock = sources.Where(p => p.StockQuantity > 0).Select(p => p.Id).ToList();
        if (sourceIdsWithStock.Count > 0 && !request.ConfirmStockLoss)
        {
            return Result.Failure<MergeProductsResultDto>(
                CatalogErrors.ProductMergeStockLossRequiresConfirmation(sourceIdsWithStock));
        }

        var group = await _db.ProductOptionGroups
            .Include(g => g.Options)
            .FirstOrDefaultAsync(
                g => g.ProductId == target.Id && g.ParentOptionId == null && g.Key == VariantOptionGroupKey,
                cancellationToken);

        if (group is null)
        {
            group = ProductOptionGroup.Create(target.Id, VariantOptionGroupKey, "Option", sortOrder: 0, isRequired: true);
            _db.ProductOptionGroups.Add(group);
        }

        // Tracked locally (not just re-queried per candidate) because two sources merged in the
        // SAME request can share an identical name/SKU seed — e.g. the exact scenario this feature
        // exists for, several duplicate "Xbox 1€" rows — and neither would be visible to a
        // database round-trip yet since nothing has been saved.
        var usedOptionValues = new HashSet<string>(group.Options.Select(o => o.Value));
        var usedSkus = new HashSet<string>(
            await _db.ProductVariants.Where(v => !v.IsDeleted).Select(v => v.Sku).ToListAsync(cancellationToken));

        var createdVariantIds = new List<Guid>();
        var sortOrder = group.Options.Count;

        foreach (var source in sources.OrderBy(p => p.Id))
        {
            var optionValue = source.NameEn.Trim().ToLowerInvariant();
            if (usedOptionValues.Contains(optionValue))
            {
                optionValue = $"{optionValue}-{source.Id:N}";
            }
            usedOptionValues.Add(optionValue);

            var option = group.AddOption(optionValue, source.NameEn, sortOrder);
            _db.ProductOptions.Add(option);

            var sku = BuildUniqueSku(source.NameEn, usedSkus);
            usedSkus.Add(sku);

            var variant = ProductVariant.Create(
                target.Id,
                sku,
                priceOverride: source.Price,
                sortOrder: sortOrder,
                lowStockThreshold: 5);
            variant.SetOptions([option.Id]);
            ApplySourceStatus(variant, source.Status);

            _db.ProductVariants.Add(variant);
            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.VariantCreated,
                productId: target.Id,
                variantId: variant.Id,
                performedByUserId: _currentUser.UserId,
                details: $"Merged from product '{source.NameEn}' ({source.Id})"));

            createdVariantIds.Add(variant.Id);
            sortOrder++;

            _db.Products.Remove(source);
        }

        // One SaveChangesAsync — every new option/variant and every source removal is part of the
        // same EF change-tracking session, so this commits atomically (all or nothing), consistent
        // with every other multi-entity handler in this module (e.g. CreateProductVariantCommandHandler).
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new MergeProductsResultDto(target.Id, createdVariantIds, sources.Count));
    }

    private static void ApplySourceStatus(ProductVariant variant, ProductStatus sourceStatus)
    {
        switch (sourceStatus)
        {
            case ProductStatus.Active:
                variant.Activate();
                break;
            case ProductStatus.Inactive:
                variant.Deactivate();
                break;
            case ProductStatus.Archived:
                variant.Archive();
                break;
            case ProductStatus.Draft:
            default:
                break;
        }
    }

    private static string BuildUniqueSku(string nameEn, HashSet<string> usedSkus)
    {
        var baseSku = SkuSlugifier.Slugify(nameEn);
        var candidate = baseSku;
        if (usedSkus.Contains(candidate))
        {
            candidate = $"{baseSku}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        return candidate.Length <= 100 ? candidate : candidate[..100];
    }
}
