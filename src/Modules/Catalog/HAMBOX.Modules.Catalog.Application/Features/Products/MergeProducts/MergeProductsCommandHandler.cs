using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;

internal sealed class MergeProductsCommandHandler : IRequestHandler<MergeProductsCommand, Result<MergeProductsResultDto>>
{
    private const string VariantOptionGroupKey = "variant";

    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICatalogSuppliersTransactionService _transaction;
    private readonly IVariantSupplierLinkMover _supplierLinks;
    private readonly ICommerceVariantRelocator _commerceRelocator;
    private readonly ILogger<MergeProductsCommandHandler> _logger;

    public MergeProductsCommandHandler(
        ICatalogDbContext db,
        ICurrentUserService currentUser,
        ICatalogSuppliersTransactionService transaction,
        IVariantSupplierLinkMover supplierLinks,
        ICommerceVariantRelocator commerceRelocator,
        ILogger<MergeProductsCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _transaction = transaction;
        _supplierLinks = supplierLinks;
        _commerceRelocator = commerceRelocator;
        _logger = logger;
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

        if (request.SourceProductIds.Contains(target.Id))
        {
            return Result.Failure<MergeProductsResultDto>(CatalogErrors.ProductPendingMergeSelfReference);
        }

        // A source that already has variants (its own stock, On-Delivery setup, supplier links...) is merged by
        // MOVING those variants under the target - see the move step below - not by creating a fresh empty one.
        var sourceVariants = await _db.ProductVariants
            .Include(v => v.SelectedOptions)
            .Where(v => !v.IsDeleted && request.SourceProductIds.Contains(v.ProductId))
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);
        var variantsBySource = sourceVariants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var movedOptionLabels = await LoadOptionLabelsAsync(sourceVariants, cancellationToken);

        // Product.StockQuantity is a bare counter with no serials/codes behind it — a variant's
        // stock is entirely code-driven (DigitalInventoryCode), so this count has no migration
        // path and would silently disappear. Require an explicit confirmation before doing that.
        // An untouched creation-default counter is a placeholder, not recorded stock, so it doesn't count.
        var sourceIdsWithStock = sources
            .Where(p => !variantsBySource.ContainsKey(p.Id))
            .Where(p => p.StockQuantity > 0 && p.StockQuantity != Product.DefaultInitialStock)
            .Select(p => p.Id)
            .ToList();
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
        var moves = new List<VariantMove>();
        var sortOrder = group.Options.Count;

        foreach (var source in sources.OrderBy(p => p.Id))
        {
            // The admin may name the variant (e.g. "PC", "Xbox") instead of reusing the source product's
            // full name, which is identical across near-duplicates and would give indistinguishable options.
            var label = request.VariantLabels is not null
                && request.VariantLabels.TryGetValue(source.Id, out var customLabel)
                && !string.IsNullOrWhiteSpace(customLabel)
                    ? customLabel.Trim()
                    : source.NameEn;

            var optionValue = label.ToLowerInvariant();
            if (usedOptionValues.Contains(optionValue))
            {
                optionValue = $"{optionValue}-{source.Id:N}";
            }
            usedOptionValues.Add(optionValue);

            if (variantsBySource.TryGetValue(source.Id, out var movingVariants))
            {
                foreach (var moving in movingVariants)
                {
                    var movedLabel = movingVariants.Count == 1
                        ? label
                        : $"{label} - {DescribeVariant(moving, movedOptionLabels)}";
                    var movedValue = movedLabel.ToLowerInvariant();
                    if (usedOptionValues.Contains(movedValue))
                    {
                        movedValue = $"{movedValue}-{moving.Id:N}";
                    }
                    usedOptionValues.Add(movedValue);

                    var movedOption = group.AddOption(movedValue, movedLabel, sortOrder);
                    _db.ProductOptions.Add(movedOption);

                    moving.MoveToProduct(target.Id, source.Price, sortOrder);
                    moving.SetOptions([movedOption.Id]);

                    _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                        InventoryAuditAction.VariantUpdated,
                        productId: target.Id,
                        variantId: moving.Id,
                        performedByUserId: _currentUser.UserId,
                        details: $"Moved from product '{source.NameEn}' ({source.Id}) into '{target.NameEn}' by merge"));

                    moves.Add(new VariantMove(moving.Id, source.Id, target.Id));
                    createdVariantIds.Add(moving.Id);
                    sortOrder++;
                }

                _db.Products.Remove(source);
                continue;
            }

            var option = group.AddOption(optionValue, label, sortOrder);
            _db.ProductOptions.Add(option);

            var sku = BuildUniqueSku(label == source.NameEn ? source.NameEn : $"{target.NameEn} {label}", usedSkus);
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
        if (moves.Count == 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Catalog and Suppliers commit together: a variant that moved but kept pointing its supplier
            // mapping at the old product would stop being fulfilled from its supplier.
            await _transaction.ExecuteAsync(
                async ct =>
                {
                    await _db.SaveChangesAsync(ct);

                    foreach (var move in moves)
                    {
                        await _db.ProductInstructions
                            .Where(i => i.VariantId == move.VariantId)
                            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ProductId, move.ToProductId), ct);
                    }

                    await _supplierLinks.MoveAsync(moves, ct);
                },
                cancellationToken);

            // Cart lines / alert subscriptions are operational, not history: a failure here must not undo
            // a merge that already committed, and the call is idempotent so it can be repeated by hand.
            foreach (var move in moves)
            {
                try
                {
                    await _commerceRelocator.MoveVariantAsync(move.VariantId, move.ToProductId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Merge moved variant {VariantId} but could not update its cart/alert rows", move.VariantId);
                }
            }
        }

        return Result.Success(new MergeProductsResultDto(target.Id, createdVariantIds, sources.Count));
    }

    private async Task<Dictionary<Guid, string>> LoadOptionLabelsAsync(
        IReadOnlyCollection<ProductVariant> variants, CancellationToken cancellationToken)
    {
        var optionIds = variants.SelectMany(v => v.SelectedOptions).Select(o => o.OptionId).Distinct().ToList();
        if (optionIds.Count == 0)
        {
            return [];
        }

        return await _db.ProductOptions
            .Where(o => optionIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Label, cancellationToken);
    }

    /// <summary>Names a moved variant after the options it used to carry, falling back to its SKU.</summary>
    private static string DescribeVariant(ProductVariant variant, IReadOnlyDictionary<Guid, string> optionLabels)
    {
        var labels = variant.SelectedOptions
            .Select(o => optionLabels.GetValueOrDefault(o.OptionId))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        return labels.Count > 0 ? string.Join(" / ", labels) : variant.Sku;
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
