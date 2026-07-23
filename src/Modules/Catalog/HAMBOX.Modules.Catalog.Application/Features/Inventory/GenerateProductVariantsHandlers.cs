using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public sealed record GenerateProductVariantsCommand(Guid ProductId)
    : IRequest<Result<GenerateProductVariantsResultDto>>;

public sealed record BulkUpdateProductVariantsCommand(
    Guid ProductId,
    IReadOnlyList<Guid> VariantIds,
    decimal? PriceOverride,
    ProductVariantStatus? Status) : IRequest<Result<int>>;

internal sealed class GenerateProductVariantsCommandHandler
    : IRequestHandler<GenerateProductVariantsCommand, Result<GenerateProductVariantsResultDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GenerateProductVariantsCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<GenerateProductVariantsResultDto>> Handle(
        GenerateProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<GenerateProductVariantsResultDto>(CatalogErrors.ProductNotFound);
        }

        var groups = await _db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => g.ProductId == request.ProductId)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);

        var rootGroups = groups.Where(g => g.ParentOptionId is null).ToList();
        if (rootGroups.Count == 0)
        {
            return Result.Failure<GenerateProductVariantsResultDto>(CatalogErrors.NoOptionGroups);
        }

        if (rootGroups.Any(group => group.Options.Count == 0))
        {
            return Result.Failure<GenerateProductVariantsResultDto>(CatalogErrors.EmptyOptionGroup);
        }

        // Recursive tree walk: root groups cross-multiply with each other (same as the old flat
        // behavior), but an option only combines with its own child groups, never a sibling
        // branch's — an empty child group just makes its parent option a leaf instead of blocking
        // generation for the whole product.
        var combinations = VariantCombinationHelper.Expand(groups);

        var optionLookup = groups
            .SelectMany(group => group.Options)
            .ToDictionary(option => option.Id);

        var existingVariants = await _db.ProductVariants
            .Include(v => v.SelectedOptions)
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        var existingKeys = existingVariants
            .ToDictionary(
                variant => VariantCombinationHelper.NormalizeCombinationKey(variant.SelectedOptions.Select(o => o.OptionId)),
                variant => variant);

        var usedSkus = existingVariants.Select(v => v.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productPrefix = product.Id.ToString("N")[..8].ToUpperInvariant();
        var createdCount = 0;
        var preservedCount = 0;
        var sortOrder = existingVariants.Count > 0 ? existingVariants.Max(v => v.SortOrder) + 1 : 0;

        foreach (var combination in combinations)
        {
            var key = VariantCombinationHelper.NormalizeCombinationKey(combination);
            if (existingKeys.ContainsKey(key))
            {
                preservedCount++;
                continue;
            }

            var optionValues = combination
                .Select(optionId => optionLookup[optionId].Value)
                .ToList();

            var sku = VariantCombinationHelper.BuildSku(productPrefix, optionValues, usedSkus);
            var variant = ProductVariant.Create(
                request.ProductId,
                sku,
                priceOverride: product.Price,
                sortOrder: sortOrder++);

            variant.SetOptions(combination);
            _db.ProductVariants.Add(variant);
            existingKeys[key] = variant;
            createdCount++;

            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.VariantCreated,
                productId: request.ProductId,
                variantId: variant.Id,
                performedByUserId: _currentUser.UserId,
                details: "Generated from option groups"));
        }

        if (createdCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new GenerateProductVariantsResultDto(
            createdCount,
            preservedCount,
            combinations.Count));
    }
}

internal sealed class BulkUpdateProductVariantsCommandHandler
    : IRequestHandler<BulkUpdateProductVariantsCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BulkUpdateProductVariantsCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(
        BulkUpdateProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.VariantIds.Count == 0)
        {
            return Result.Success(0);
        }

        if (request.PriceOverride is null && request.Status is null)
        {
            return Result.Success(0);
        }

        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted && request.VariantIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return Result.Success(0);
        }

        foreach (var variant in variants)
        {
            var status = request.Status ?? variant.Status;
            var isVisible = status == ProductVariantStatus.Active;

            variant.Update(
                variant.Sku,
                variant.PlanId,
                request.PriceOverride ?? variant.PriceOverride,
                variant.ComparePrice,
                variant.SortOrder,
                status,
                isVisible,
                variant.MembershipPlanId,
                variant.LowStockThreshold);
        }

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantUpdated,
            productId: request.ProductId,
            performedByUserId: _currentUser.UserId,
            details: $"Bulk updated {variants.Count} variants"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(variants.Count);
    }
}
