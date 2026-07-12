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

public sealed record GetInventoryStatisticsQuery(Guid? ProductId, Guid? VariantId) : IRequest<Result<InventoryStatisticsDto>>;

internal sealed class GetInventoryStatisticsQueryHandler : IRequestHandler<GetInventoryStatisticsQuery, Result<InventoryStatisticsDto>>
{
    private readonly IInventoryEngine _engine;

    public GetInventoryStatisticsQueryHandler(IInventoryEngine engine) => _engine = engine;

    public async Task<Result<InventoryStatisticsDto>> Handle(GetInventoryStatisticsQuery request, CancellationToken cancellationToken)
    {
        var stats = await _engine.GetStatisticsAsync(request.ProductId, request.VariantId, cancellationToken);
        return Result.Success(new InventoryStatisticsDto(
            stats.Available, stats.Reserved, stats.Sold, stats.Expired,
            stats.LowStockVariants, stats.OutOfStockVariants,
            stats.InventoryValue, stats.PurchaseCost, stats.EstimatedRevenue, stats.EstimatedProfit));
    }
}

public sealed record GetProductVariantsQuery(Guid ProductId) : IRequest<Result<IReadOnlyList<ProductVariantDto>>>;

internal sealed class GetProductVariantsQueryHandler : IRequestHandler<GetProductVariantsQuery, Result<IReadOnlyList<ProductVariantDto>>>
{
    private readonly ICatalogDbContext _db;
    private readonly IInventoryEngine _engine;

    public GetProductVariantsQueryHandler(ICatalogDbContext db, IInventoryEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<Result<IReadOnlyList<ProductVariantDto>>> Handle(GetProductVariantsQuery request, CancellationToken cancellationToken)
    {
        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        var variantIds = variants.Select(v => v.Id).ToList();
        var stock = await _engine.GetVariantStockBulkAsync(variantIds, cancellationToken);

        var codeCounts = variantIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.DigitalInventoryCodes
                .AsNoTracking()
                .Where(code => variantIds.Contains(code.VariantId))
                .GroupBy(code => code.VariantId)
                .Select(group => new { VariantId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(entry => entry.VariantId, entry => entry.Count, cancellationToken);

        var dtos = variants.Select(v =>
        {
            stock.TryGetValue(v.Id, out var s);
            codeCounts.TryGetValue(v.Id, out var totalCodes);
            return new ProductVariantDto(
                v.Id, v.ProductId, v.PlanId, v.Sku, v.PriceOverride, v.ComparePrice,
                v.SortOrder, v.Status.ToString(), v.IsVisible, v.MembershipPlanId, v.LowStockThreshold,
                s?.Available ?? 0, s?.Reserved ?? 0, s?.Sold ?? 0, totalCodes,
                s?.IsLowStock ?? false, s?.IsOutOfStock ?? true,
                v.SelectedOptions.Select(o => o.OptionId).ToList());
        }).ToList();

        return Result.Success<IReadOnlyList<ProductVariantDto>>(dtos);
    }
}

public sealed record CreateProductVariantCommand(
    Guid ProductId,
    string Sku,
    Guid? PlanId,
    decimal? PriceOverride,
    decimal? ComparePrice,
    int SortOrder,
    Guid? MembershipPlanId,
    int LowStockThreshold,
    IReadOnlyList<Guid> OptionIds) : IRequest<Result<Guid>>;

internal sealed class CreateProductVariantCommandHandler : IRequestHandler<CreateProductVariantCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateProductVariantCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var productExists = await _db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure<Guid>(CatalogErrors.ProductNotFound);
        }

        var variant = ProductVariant.Create(
            request.ProductId,
            request.Sku,
            request.PlanId,
            request.PriceOverride,
            request.ComparePrice,
            request.SortOrder,
            request.LowStockThreshold);

        variant.SetOptions(request.OptionIds);

        var requestedOptionIds = request.OptionIds.Distinct().OrderBy(id => id).ToList();
        var existingVariants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        var isDuplicate = existingVariants.Any(existing =>
        {
            var existingIds = existing.SelectedOptions.Select(o => o.OptionId).OrderBy(id => id).ToList();
            return existingIds.SequenceEqual(requestedOptionIds);
        });

        if (isDuplicate)
        {
            return Result.Failure<Guid>(CatalogErrors.DuplicateVariantCombination);
        }

        _db.ProductVariants.Add(variant);
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantCreated,
            productId: request.ProductId,
            variantId: variant.Id,
            performedByUserId: _currentUser.UserId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(variant.Id);
    }
}

public sealed record ActivateProductVariantsCommand(Guid ProductId) : IRequest<Result<int>>;

internal sealed class ActivateProductVariantsCommandHandler : IRequestHandler<ActivateProductVariantsCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ActivateProductVariantsCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(ActivateProductVariantsCommand request, CancellationToken cancellationToken)
    {
        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return Result.Success(0);
        }

        foreach (var variant in variants)
        {
            variant.Activate();
        }

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantUpdated,
            productId: request.ProductId,
            performedByUserId: _currentUser.UserId,
            details: $"Activated {variants.Count} variants"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(variants.Count);
    }
}

public sealed record GetProductOptionGroupsQuery(Guid ProductId) : IRequest<Result<IReadOnlyList<ProductOptionGroupDto>>>;

internal sealed class GetProductOptionGroupsQueryHandler : IRequestHandler<GetProductOptionGroupsQuery, Result<IReadOnlyList<ProductOptionGroupDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductOptionGroupsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<ProductOptionGroupDto>>> Handle(GetProductOptionGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => g.ProductId == request.ProductId)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);

        var dtos = groups.Select(g => new ProductOptionGroupDto(
            g.Id, g.ProductId, g.Key, g.DisplayName, g.SortOrder, g.IsRequired,
            g.Options.OrderBy(o => o.SortOrder).Select(o => new ProductOptionDto(o.Id, o.OptionGroupId, o.Value, o.Label, o.SortOrder)).ToList()
        )).ToList();

        return Result.Success<IReadOnlyList<ProductOptionGroupDto>>(dtos);
    }
}

public sealed record CreateProductOptionGroupCommand(
    Guid ProductId,
    string Key,
    string DisplayName,
    int SortOrder,
    bool IsRequired) : IRequest<Result<Guid>>;

internal sealed class CreateProductOptionGroupCommandHandler : IRequestHandler<CreateProductOptionGroupCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateProductOptionGroupCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateProductOptionGroupCommand request, CancellationToken cancellationToken)
    {
        var group = ProductOptionGroup.Create(request.ProductId, request.Key, request.DisplayName, request.SortOrder, request.IsRequired);
        _db.ProductOptionGroups.Add(group);
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.OptionGroupCreated,
            productId: request.ProductId,
            performedByUserId: _currentUser.UserId,
            details: request.Key));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(group.Id);
    }
}

public sealed record CreateProductOptionCommand(Guid OptionGroupId, string Value, string Label, int SortOrder) : IRequest<Result<Guid>>;

internal sealed class CreateProductOptionCommandHandler : IRequestHandler<CreateProductOptionCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateProductOptionCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateProductOptionCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.ProductOptionGroups.FirstOrDefaultAsync(g => g.Id == request.OptionGroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<Guid>(CatalogErrors.ProductNotFound);
        }

        var option = ProductOption.Create(request.OptionGroupId, request.Value, request.Label, request.SortOrder);
        _db.ProductOptions.Add(option);
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.OptionCreated,
            productId: group.ProductId,
            performedByUserId: _currentUser.UserId,
            details: request.Value));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(option.Id);
    }
}
