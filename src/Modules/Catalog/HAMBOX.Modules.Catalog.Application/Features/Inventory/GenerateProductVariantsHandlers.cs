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

/// <param name="ProductId">The product.</param>
/// <param name="Selected">
/// The exact combinations to create (each an array of option ids). Null keeps the original behaviour of creating every
/// missing combination. Only complete combinations of this product are accepted; ones that already exist are skipped.
/// </param>
public sealed record GenerateProductVariantsCommand(Guid ProductId, IReadOnlyList<IReadOnlyList<Guid>>? Selected = null)
    : IRequest<Result<GenerateProductVariantsResultDto>>;

public sealed record GetVariantCombinationsQuery(Guid ProductId) : IRequest<Result<VariantCombinationsDto>>;

internal sealed class GetVariantCombinationsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetVariantCombinationsQuery, Result<VariantCombinationsDto>>
{
    public async Task<Result<VariantCombinationsDto>> Handle(GetVariantCombinationsQuery request, CancellationToken cancellationToken)
    {
        var groups = await db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => g.ProductId == request.ProductId)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);

        if (VariantCombinationHelper.CountCombinations(groups) > VariantCombinationHelper.MaxVariantsPerProduct)
        {
            return Result.Failure<VariantCombinationsDto>(CatalogErrors.TooManyVariantCombinations);
        }

        var existingKeys = (await db.ProductVariants
                .AsNoTracking()
                .Include(v => v.SelectedOptions)
                .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
                .ToListAsync(cancellationToken))
            .Select(v => VariantCombinationHelper.NormalizeCombinationKey(v.SelectedOptions.Select(o => o.OptionId)))
            .ToHashSet();

        var items = VariantCombinationHelper.Expand(groups)
            .Select(path => new VariantCombinationDto(
                path.ToList(),
                VariantCompleteness.Labels(path, groups),
                existingKeys.Contains(VariantCombinationHelper.NormalizeCombinationKey(path))))
            .ToList();

        var existing = items.Count(i => i.Exists);
        return Result.Success(new VariantCombinationsDto(items, existing, items.Count - existing));
    }
}

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
        if (VariantCombinationHelper.CountCombinations(groups) > VariantCombinationHelper.MaxVariantsPerProduct)
        {
            return Result.Failure<GenerateProductVariantsResultDto>(CatalogErrors.TooManyVariantCombinations);
        }

        var combinations = VariantCombinationHelper.Expand(groups);
        if (request.Selected is not null)
        {
            // Only the combinations the admin ticked, and only ones that really are complete combinations of this product.
            var wanted = request.Selected.Select(VariantCombinationHelper.NormalizeCombinationKey).ToHashSet();
            combinations = combinations.Where(c => wanted.Contains(VariantCombinationHelper.NormalizeCombinationKey(c))).ToList();
        }

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
                variant.LowStockThreshold,
                variant.CostPrice,
                variant.MemberPrice);
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
