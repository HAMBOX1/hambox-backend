using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Application.Features.Products.AssignProductCollection;
using HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;
using HAMBOX.Modules.Catalog.Application.Features.Products.RemoveProductCollection;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
// PublishProductCommand / DeactivateProductCommand / ArchiveProductCommand live directly in Features.Products (ProductLifecycleHandlers.cs).
using HAMBOX.Modules.Catalog.Application.Features.Products;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;

internal sealed class BulkProductsCommandHandler : IRequestHandler<BulkProductsCommand, Result<BulkProductsResultDto>>
{
    /// <summary>Products handled per database round-trip by the batched actions.</summary>
    private const int BatchSize = 200;

    /// <summary>Matches the Product.BadgeTextEn/Ar column length.</summary>
    private const int ProductBadgeMaxLength = 60;

    /// <summary>Highest "-N%" a bulk old price may show; keeps the old price from becoming absurdly large.</summary>
    private const decimal MaxOldPriceDiscountPercent = 95m;

    private readonly ICatalogDbContext _db;
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public BulkProductsCommandHandler(ICatalogDbContext db, ISender sender, ICurrentUserService currentUser)
    {
        _db = db;
        _sender = sender;
        _currentUser = currentUser;
    }

    public async Task<Result<BulkProductsResultDto>> Handle(BulkProductsCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();

        if (action.Length == 0)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "set-badge"
            && ((request.BadgeTextEn?.Trim().Length ?? 0) > ProductBadgeMaxLength
                || (request.BadgeTextAr?.Trim().Length ?? 0) > ProductBadgeMaxLength))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBadgeTooLong);
        }

        if (action == "change-category" && request.TargetCategoryId is null)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "adjust-price" && (request.PriceMode is null || request.PriceValue is null))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "adjust-price" && request.PriceValue is < 0 or > ProductPublishGuard.MaxPrice)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.InvalidPriceAdjustment);
        }

        if (action == "adjust-price" && request.PriceMode == PriceAdjustmentMode.DecreasePercent && request.PriceValue > 100)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.InvalidPriceAdjustment);
        }

        if ((action == "assign-collection" || action == "remove-collection") && request.TargetCollectionId is null)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "set-fulfillment" && request.TargetFulfillmentMode is null)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "set-margin" && (request.PriceValue is null || request.PriceValue < 0 || request.PriceValue >= 100))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.InvalidPriceAdjustment);
        }

        // "set-old-price": PriceValue is the discount the customer should see (e.g. 20 = "-20%"); 0 removes the old price.
        if (action == "set-old-price" && (request.PriceValue is null || request.PriceValue < 0 || request.PriceValue > MaxOldPriceDiscountPercent))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.InvalidPriceAdjustment);
        }

        var selection = new BulkProductSelection(
            request.ProductIds, request.SearchTerm, request.Status, request.CategoryId, request.SelectAllMatching);

        var resolved = await BulkProductSelectionResolver.ResolveAsync(_db, selection, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<BulkProductsResultDto>(resolved.Error);
        }

        // These actions touch many rows of the same kind, so they run in batches (one query and one save per
        // BatchSize products) instead of one MediatR round-trip per product, which is what made large
        // selections crawl.
        switch (action)
        {
            case "assign-collection":
                return await AssignCollectionAsync(resolved.Value, request.TargetCollectionId!.Value, cancellationToken);
            case "remove-collection":
                return await RemoveCollectionAsync(resolved.Value, request.TargetCollectionId!.Value, cancellationToken);
            case "adjust-price":
                return await AdjustPriceAsync(resolved.Value, request.PriceMode!.Value, request.PriceValue!.Value, cancellationToken);
            case "set-fulfillment":
                return await SetFulfillmentAsync(resolved.Value, request.TargetFulfillmentMode!.Value, cancellationToken);
            case "set-margin":
                return await SetMarginAsync(resolved.Value, request.PriceValue!.Value, cancellationToken);
            case "set-badge":
                return await SetBadgeAsync(resolved.Value, request.BadgeTextEn, request.BadgeTextAr, request.BadgeColor, cancellationToken);
            case "set-old-price":
                return await SetOldPriceAsync(resolved.Value, request.PriceValue!.Value, cancellationToken);
        }

        var success = 0;
        var errors = new List<string>();

        foreach (var productId in resolved.Value)
        {
            var result = action switch
            {
                "publish" => await _sender.Send(new PublishProductCommand(productId), cancellationToken),
                "unpublish" => await _sender.Send(new DeactivateProductCommand(productId), cancellationToken),
                "archive" => await _sender.Send(new ArchiveProductCommand(productId), cancellationToken),
                "change-category" => await _sender.Send(
                    new ChangeProductCategoryCommand(productId, request.TargetCategoryId!.Value), cancellationToken),
                "adjust-price" => await _sender.Send(
                    new AdjustProductPriceCommand(productId, request.PriceMode!.Value, request.PriceValue!.Value), cancellationToken),
                "assign-collection" => await _sender.Send(
                    new AssignProductCollectionCommand(productId, request.TargetCollectionId!.Value), cancellationToken),
                "remove-collection" => await _sender.Send(
                    new RemoveProductCollectionCommand(productId, request.TargetCollectionId!.Value), cancellationToken),
                _ => Result.Failure(CatalogErrors.ProductBulkActionNotSupported(action)),
            };

            if (result.IsSuccess)
            {
                success++;
            }
            else
            {
                errors.Add($"{productId}: {result.Error.Description}");
            }
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    private async Task<Result<BulkProductsResultDto>> AssignCollectionAsync(
        IReadOnlyList<Guid> productIds, Guid collectionId, CancellationToken ct)
    {
        if (!await _db.ProductCollections.AnyAsync(c => c.Id == collectionId, ct))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.CollectionNotFound);
        }

        var success = 0;
        var errors = new List<string>();

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var products = await _db.Products
                .Include(p => p.Collections)
                .Where(p => ids.Contains(p.Id))
                .ToListAsync(ct);
            var found = products.Select(p => p.Id).ToHashSet();

            foreach (var id in ids.Where(id => !found.Contains(id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                var item = product.AddToCollection(collectionId);
                if (item is not null)
                {
                    _db.ProductCollectionItems.Add(item);
                }

                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    private async Task<Result<BulkProductsResultDto>> RemoveCollectionAsync(
        IReadOnlyList<Guid> productIds, Guid collectionId, CancellationToken ct)
    {
        var success = 0;
        var errors = new List<string>();

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var products = await _db.Products
                .Include(p => p.Collections)
                .Where(p => ids.Contains(p.Id))
                .ToListAsync(ct);
            var found = products.Select(p => p.Id).ToHashSet();

            foreach (var id in ids.Where(id => !found.Contains(id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                var removed = product.RemoveFromCollection(collectionId);
                if (removed is not null)
                {
                    _db.ProductCollectionItems.Remove(removed);
                }

                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    private async Task<Result<BulkProductsResultDto>> AdjustPriceAsync(
        IReadOnlyList<Guid> productIds, PriceAdjustmentMode mode, decimal value, CancellationToken ct)
    {
        var success = 0;
        var errors = new List<string>();

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            var found = products.Select(p => p.Id).ToHashSet();
            // Variants that carry their own sale price are adjusted too, otherwise they would silently keep the old price.
            var ownPriced = (await _db.ProductVariants
                    .Where(v => ids.Contains(v.ProductId) && !v.IsDeleted && v.PriceOverride != null)
                    .ToListAsync(ct))
                .ToLookup(v => v.ProductId);

            foreach (var id in ids.Where(id => !found.Contains(id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            decimal Adjusted(decimal current) => mode switch
            {
                PriceAdjustmentMode.IncreasePercent => current * (1 + value / 100m),
                PriceAdjustmentMode.DecreasePercent => current * (1 - value / 100m),
                PriceAdjustmentMode.SetFixed => value,
                _ => current,
            };

            foreach (var product in products)
            {
                var newPrice = Adjusted(product.Price);

                var priceError = ValidateNewPrice(product, newPrice);
                if (priceError is null)
                {
                    foreach (var variant in ownPriced[product.Id])
                    {
                        priceError = ValidateNewPrice(product, Adjusted(variant.PriceOverride!.Value));
                        if (priceError is not null)
                        {
                            break;
                        }
                    }
                }

                if (priceError is not null)
                {
                    errors.Add($"{product.Id}: {priceError.Description}");
                    continue;
                }

                product.ChangePrice(decimal.Round(newPrice, 2));
                foreach (var variant in ownPriced[product.Id])
                {
                    variant.SetPriceOverride(decimal.Round(Adjusted(variant.PriceOverride!.Value), 2));
                }

                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    private async Task<Result<BulkProductsResultDto>> SetFulfillmentAsync(
        IReadOnlyList<Guid> productIds, FulfillmentMode mode, CancellationToken ct)
    {
        var success = 0;
        var errors = new List<string>();

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var variants = await _db.ProductVariants
                .Where(v => ids.Contains(v.ProductId) && !v.IsDeleted)
                .ToListAsync(ct);
            var withVariants = variants.Select(v => v.ProductId).ToHashSet();

            foreach (var variant in variants)
            {
                var previous = variant.SetFulfillmentMode(mode);
                if (previous == mode)
                {
                    continue;
                }

                _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                    InventoryAuditAction.FulfillmentModeChanged,
                    productId: variant.ProductId,
                    variantId: variant.Id,
                    performedByUserId: _currentUser.UserId,
                    details: $"{{\"previous\":\"{previous}\",\"new\":\"{mode}\",\"bulk\":true}}"));
            }

            foreach (var id in ids)
            {
                if (withVariants.Contains(id))
                {
                    success++;
                }
                else
                {
                    errors.Add($"{id}: This product has no variants to change.");
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    private async Task<Result<BulkProductsResultDto>> SetBadgeAsync(
        IReadOnlyList<Guid> productIds, string? textEn, string? textAr, string? color, CancellationToken ct)
    {
        var success = 0;
        var errors = new List<string>();

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            var found = products.Select(p => p.Id).ToHashSet();

            foreach (var id in ids.Where(id => !found.Contains(id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                product.SetBadge(textEn, textAr, color);
                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    private async Task<Result<BulkProductsResultDto>> SetMarginAsync(
        IReadOnlyList<Guid> productIds, decimal marginPercent, CancellationToken ct)
    {
        var success = 0;
        var errors = new List<string>();
        var factor = 1m - marginPercent / 100m;

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            var variants = await _db.ProductVariants
                .Where(v => ids.Contains(v.ProductId) && !v.IsDeleted)
                .ToListAsync(ct);
            var byProduct = variants.ToLookup(v => v.ProductId);

            foreach (var id in ids.Where(id => products.All(p => p.Id != id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                // A variant with its own sale price is priced from its own cost; the rest follow the product's price,
                // which comes from the cheapest cost among them. Variants with no cost price are left as they are.
                var own = byProduct[product.Id].Where(v => v.PriceOverride.HasValue && v.CostPrice.HasValue).ToList();
                var inheriting = byProduct[product.Id]
                    .Where(v => !v.PriceOverride.HasValue && v.CostPrice.HasValue)
                    .Select(v => v.CostPrice!.Value)
                    .ToList();
                if (own.Count == 0 && inheriting.Count == 0)
                {
                    errors.Add($"{product.Id}: No cost price is set, so a margin cannot be applied.");
                    continue;
                }

                var productPrice = inheriting.Count > 0 ? decimal.Round(inheriting.Min() / factor, 2) : (decimal?)null;
                var variantPrices = own.Select(v => (Variant: v, Price: decimal.Round(v.CostPrice!.Value / factor, 2))).ToList();

                var marginError = productPrice is { } p1 ? ValidateNewPrice(product, p1) : null;
                foreach (var (_, price) in variantPrices)
                {
                    marginError ??= ValidateNewPrice(product, price);
                }

                if (marginError is not null)
                {
                    errors.Add($"{product.Id}: {marginError.Description}");
                    continue;
                }

                if (productPrice is { } newProductPrice)
                {
                    product.ChangePrice(newProductPrice);
                }

                foreach (var (variant, price) in variantPrices)
                {
                    variant.SetPriceOverride(price);
                }

                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    /// <summary>
    /// Gives every variant of the selected products an old (compare-at) price so that the storefront shows it struck
    /// through with the chosen "-N%": old price = sale price ÷ (1 − N/100). A percent of 0 removes the old price. The sale
    /// price a variant is sold at (its own, else the product's) is never changed.
    /// </summary>
    private async Task<Result<BulkProductsResultDto>> SetOldPriceAsync(
        IReadOnlyList<Guid> productIds, decimal discountPercent, CancellationToken ct)
    {
        var success = 0;
        var errors = new List<string>();
        var factor = 1m - discountPercent / 100m;

        foreach (var chunk in productIds.Chunk(BatchSize))
        {
            var ids = chunk.ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            var variants = await _db.ProductVariants.Where(v => ids.Contains(v.ProductId) && !v.IsDeleted).ToListAsync(ct);
            var byProduct = variants.ToLookup(v => v.ProductId);

            foreach (var id in ids.Where(id => products.All(p => p.Id != id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                var productVariants = byProduct[product.Id].ToList();
                if (productVariants.Count == 0)
                {
                    errors.Add($"{product.Id}: It has no variants to put an old price on.");
                    continue;
                }

                // Work every variant out first, so a product is either fully updated or left untouched.
                var planned = new List<(ProductVariant Variant, decimal? OldPrice)>();
                string? problem = null;
                foreach (var variant in productVariants)
                {
                    if (discountPercent == 0m)
                    {
                        planned.Add((variant, null));
                        continue;
                    }

                    var salePrice = variant.PriceOverride ?? product.Price;
                    var oldPrice = decimal.Round(salePrice / factor, 2);
                    if (salePrice <= 0)
                    {
                        problem = "A variant has no sale price yet.";
                        break;
                    }

                    if (oldPrice > ProductPublishGuard.MaxPrice)
                    {
                        problem = CatalogErrors.ProductPriceTooHigh.Description;
                        break;
                    }

                    planned.Add((variant, oldPrice));
                }

                if (problem is not null)
                {
                    errors.Add($"{product.Id}: {problem}");
                    continue;
                }

                foreach (var (variant, oldPrice) in planned)
                {
                    variant.SetComparePrice(oldPrice);
                }

                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }

    /// <summary>Shared price rules for the bulk price actions: non-negative, under the ceiling, and a live product stays above zero.</summary>
    private static Error? ValidateNewPrice(Product product, decimal newPrice)
    {
        if (newPrice < 0)
        {
            return CatalogErrors.InvalidPriceAdjustment;
        }

        if (newPrice > ProductPublishGuard.MaxPrice)
        {
            return CatalogErrors.ProductPriceTooHigh;
        }

        return newPrice <= 0 && product.Status == ProductStatus.Active ? CatalogErrors.ProductNotPublishablePrice : null;
    }
}
