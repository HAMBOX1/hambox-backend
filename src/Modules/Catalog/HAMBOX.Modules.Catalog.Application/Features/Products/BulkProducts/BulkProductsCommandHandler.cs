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
        var action = request.Action.Trim().ToLowerInvariant();

        if (action == "change-category" && request.TargetCategoryId is null)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "adjust-price" && (request.PriceMode is null || request.PriceValue is null))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
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
                return await SetBadgeAsync(resolved.Value, request.BadgeTextEn, request.BadgeTextAr, cancellationToken);
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

            foreach (var id in ids.Where(id => !found.Contains(id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                var newPrice = mode switch
                {
                    PriceAdjustmentMode.IncreasePercent => product.Price * (1 + value / 100m),
                    PriceAdjustmentMode.DecreasePercent => product.Price * (1 - value / 100m),
                    PriceAdjustmentMode.SetFixed => value,
                    _ => product.Price,
                };

                if (newPrice < 0)
                {
                    errors.Add($"{product.Id}: {CatalogErrors.InvalidPriceAdjustment.Description}");
                    continue;
                }

                product.ChangePrice(decimal.Round(newPrice, 2));
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
        IReadOnlyList<Guid> productIds, string? textEn, string? textAr, CancellationToken ct)
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
                product.SetBadge(textEn, textAr);
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
                .Select(v => new { v.ProductId, v.CostPrice, v.PriceOverride })
                .ToListAsync(ct);
            var byProduct = variants.ToLookup(v => v.ProductId);

            foreach (var id in ids.Where(id => products.All(p => p.Id != id)))
            {
                errors.Add($"{id}: {CatalogErrors.ProductNotFound.Description}");
            }

            foreach (var product in products)
            {
                var costs = byProduct[product.Id].Where(v => v.CostPrice.HasValue).Select(v => v.CostPrice!.Value).ToList();
                if (costs.Count == 0)
                {
                    errors.Add($"{product.Id}: No cost price is set, so a margin cannot be applied.");
                    continue;
                }

                if (byProduct[product.Id].Any(v => v.PriceOverride.HasValue))
                {
                    errors.Add($"{product.Id}: Its variants have their own sale prices; edit those instead.");
                    continue;
                }

                product.ChangePrice(decimal.Round(costs.Min() / factor, 2));
                success++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }
}
