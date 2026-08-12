using System.Text;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public sealed record UpdateProductVariantCommand(
    Guid VariantId,
    string Sku,
    Guid? PlanId,
    decimal? PriceOverride,
    decimal? ComparePrice,
    int SortOrder,
    ProductVariantStatus Status,
    bool IsVisible,
    Guid? MembershipPlanId,
    int LowStockThreshold,
    IReadOnlyList<Guid> OptionIds) : IRequest<Result>;

public sealed record DeleteProductVariantCommand(Guid VariantId) : IRequest<Result>;
public sealed record DuplicateProductVariantCommand(Guid VariantId, string? SkuSuffix) : IRequest<Result<Guid>>;
public sealed record ActivateProductVariantCommand(Guid VariantId) : IRequest<Result>;
public sealed record DeactivateProductVariantCommand(Guid VariantId) : IRequest<Result>;

/// <summary>
/// The primary "take this variant off sale" admin action — reversible via
/// <see cref="ActivateProductVariantCommand"/>. Distinct from <see cref="DeleteProductVariantCommand"/>,
/// which is the permanent, irreversible-in-practice tombstone gated by usage inspection.
/// </summary>
public sealed record ArchiveProductVariantCommand(Guid VariantId) : IRequest<Result>;

public sealed record UpdateProductOptionGroupCommand(
    Guid GroupId,
    string DisplayName,
    int SortOrder,
    bool IsRequired) : IRequest<Result>;

public sealed record DeleteProductOptionGroupCommand(Guid GroupId, bool Force = false) : IRequest<Result>;
public sealed record ReorderProductOptionGroupsCommand(Guid ProductId, IReadOnlyList<Guid> OrderedGroupIds) : IRequest<Result>;

public sealed record UpdateProductOptionCommand(Guid OptionId, string Label, int SortOrder) : IRequest<Result>;
public sealed record DeleteProductOptionCommand(Guid OptionId) : IRequest<Result>;
public sealed record ReorderProductOptionsCommand(Guid GroupId, IReadOnlyList<Guid> OrderedOptionIds) : IRequest<Result>;

public sealed record DisableInventoryCodeCommand(Guid CodeId) : IRequest<Result>;
public sealed record EnableInventoryCodeCommand(Guid CodeId) : IRequest<Result>;
public sealed record DeleteInventoryCodeCommand(Guid CodeId) : IRequest<Result>;
public sealed record RevealInventoryCodeCommand(Guid CodeId, string IpAddress, string UserAgent)
    : IRequest<Result<RevealInventoryCodeDto>>;
public sealed record BulkDisableInventoryCodesCommand(Guid VariantId, IReadOnlyList<Guid> CodeIds) : IRequest<Result<int>>;
public sealed record BulkDeleteInventoryCodesCommand(Guid VariantId, IReadOnlyList<Guid> CodeIds) : IRequest<Result<int>>;

public sealed record ExportInventoryCodesQuery(Guid VariantId, string? Status) : IRequest<Result<ExportInventoryCodesDto>>;

public sealed record ExportInventoryCodesDto(string FileName, string ContentType, byte[] Content);

/// <summary>
/// <paramref name="BlockedVariantIds"/> is populated only by bulk-delete (empty for bulk-duplicate)
/// so the admin UI can offer "inspect usage" for exactly the variants that were blocked, instead of
/// just a formatted error string.
/// </summary>
public sealed record BulkVariantsResultDto(
    int SuccessCount,
    int ErrorCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<Guid> BlockedVariantIds);
public sealed record BulkDeleteProductVariantsCommand(Guid ProductId, IReadOnlyList<Guid> VariantIds) : IRequest<Result<BulkVariantsResultDto>>;
public sealed record BulkDuplicateProductVariantsCommand(Guid ProductId, IReadOnlyList<Guid> VariantIds) : IRequest<Result<BulkVariantsResultDto>>;
public sealed record ExportVariantsInventoryCodesQuery(Guid ProductId, IReadOnlyList<Guid> VariantIds, string? Status) : IRequest<Result<ExportInventoryCodesDto>>;

internal sealed class UpdateProductVariantCommandHandler : IRequestHandler<UpdateProductVariantCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateProductVariantCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants
            .Include(v => v.SelectedOptions)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId && !v.IsDeleted, cancellationToken);

        if (variant is null)
        {
            return Result.Failure(CatalogErrors.VariantNotFound);
        }

        var requestedOptionIds = request.OptionIds.Distinct().OrderBy(id => id).ToList();
        var existingVariants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .Where(v => v.ProductId == variant.ProductId && v.Id != variant.Id && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (existingVariants.Any(existing =>
            {
                var existingIds = existing.SelectedOptions.Select(o => o.OptionId).OrderBy(id => id).ToList();
                return existingIds.SequenceEqual(requestedOptionIds);
            }))
        {
            return Result.Failure(CatalogErrors.DuplicateVariantCombination);
        }

        variant.Update(
            request.Sku,
            request.PlanId,
            request.PriceOverride,
            request.ComparePrice,
            request.SortOrder,
            request.Status,
            request.IsVisible,
            request.MembershipPlanId,
            request.LowStockThreshold);

        variant.SetOptions(requestedOptionIds);

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantUpdated,
            productId: variant.ProductId,
            variantId: variant.Id,
            performedByUserId: _currentUser.UserId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// The permanent, irreversible-in-practice "Delete Permanently" action — distinct from
/// <see cref="ArchiveProductVariantCommand"/>, which is the everyday reversible action. Only
/// succeeds when a fresh, transactionally re-checked usage inspection proves zero protected
/// history AND zero un-cleaned-up removable data; never trusts counts the caller already saw.
/// The actual check/lock/delete sequence lives in <see cref="IInventoryEngine.DeleteVariantPermanentlyAsync"/>
/// (Infrastructure), since it needs a real DB transaction + row lock that this layer cannot open.
/// </summary>
internal sealed class DeleteProductVariantCommandHandler : IRequestHandler<DeleteProductVariantCommand, Result>
{
    private readonly IInventoryEngine _engine;

    public DeleteProductVariantCommandHandler(IInventoryEngine engine) => _engine = engine;

    public async Task<Result> Handle(DeleteProductVariantCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.DeleteVariantPermanentlyAsync(request.VariantId, cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Variant not found.")
        {
            return Result.Failure(CatalogErrors.VariantNotFound);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Variant has protected usage.")
        {
            return Result.Failure(CatalogErrors.VariantHasProtectedUsage);
        }
    }
}

internal sealed class DuplicateProductVariantCommandHandler : IRequestHandler<DuplicateProductVariantCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DuplicateProductVariantCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(DuplicateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var source = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId && !v.IsDeleted, cancellationToken);

        if (source is null)
        {
            return Result.Failure<Guid>(CatalogErrors.VariantNotFound);
        }

        var suffix = string.IsNullOrWhiteSpace(request.SkuSuffix) ? "-COPY" : request.SkuSuffix.Trim();
        var duplicateSku = BuildDuplicateSku(source.Sku, suffix);
        var optionIds = source.SelectedOptions.Select(o => o.OptionId).ToList();

        var duplicate = ProductVariant.Create(
            source.ProductId,
            duplicateSku,
            source.PlanId,
            source.PriceOverride,
            source.ComparePrice,
            source.SortOrder + 1,
            source.LowStockThreshold);

        duplicate.Update(
            duplicateSku,
            source.PlanId,
            source.PriceOverride,
            source.ComparePrice,
            source.SortOrder + 1,
            ProductVariantStatus.Draft,
            false,
            source.MembershipPlanId,
            source.LowStockThreshold);

        duplicate.SetOptions(optionIds);
        _db.ProductVariants.Add(duplicate);

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantCreated,
            productId: source.ProductId,
            variantId: duplicate.Id,
            performedByUserId: _currentUser.UserId,
            details: $"Duplicated from variant {source.Id}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(duplicate.Id);
    }

    private static string BuildDuplicateSku(string sourceSku, string suffix)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var candidate = $"{sourceSku}{suffix}-{token}";
        return candidate.Length <= 100 ? candidate : candidate[..100];
    }
}

internal sealed class ActivateProductVariantCommandHandler : IRequestHandler<ActivateProductVariantCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ActivateProductVariantCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ActivateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.VariantId && !v.IsDeleted, cancellationToken);
        if (variant is null)
        {
            return Result.Failure(CatalogErrors.VariantNotFound);
        }

        variant.Activate();
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantUpdated,
            productId: variant.ProductId,
            variantId: variant.Id,
            performedByUserId: _currentUser.UserId,
            details: "Activated variant"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DeactivateProductVariantCommandHandler : IRequestHandler<DeactivateProductVariantCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeactivateProductVariantCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeactivateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.VariantId && !v.IsDeleted, cancellationToken);
        if (variant is null)
        {
            return Result.Failure(CatalogErrors.VariantNotFound);
        }

        variant.Deactivate();
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantUpdated,
            productId: variant.ProductId,
            variantId: variant.Id,
            performedByUserId: _currentUser.UserId,
            details: "Deactivated variant"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// The primary "Delete" action surfaced to admins day-to-day: reversible via
/// <see cref="ActivateProductVariantCommand"/>, preserves every historical record, and never
/// requires usage inspection since nothing is removed or destroyed.
/// </summary>
internal sealed class ArchiveProductVariantCommandHandler : IRequestHandler<ArchiveProductVariantCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ArchiveProductVariantCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ArchiveProductVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.VariantId && !v.IsDeleted, cancellationToken);
        if (variant is null)
        {
            return Result.Failure(CatalogErrors.VariantNotFound);
        }

        variant.Archive();
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.VariantArchived,
            productId: variant.ProductId,
            variantId: variant.Id,
            performedByUserId: _currentUser.UserId,
            details: "Archived variant"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class UpdateProductOptionGroupCommandHandler : IRequestHandler<UpdateProductOptionGroupCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public UpdateProductOptionGroupCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateProductOptionGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.ProductOptionGroups.FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(CatalogErrors.OptionGroupNotFound);
        }

        group.Update(request.DisplayName, request.SortOrder, request.IsRequired);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Deleting an option group cascade-removes every ProductOption in it, which cascade-removes every
/// ProductVariantOption row that referenced one of those options (a real DB FK — see
/// InventoryConfigurations). That would silently strip a variant's identifying option combination
/// out from under it regardless of the variant's own protected-history status, so this handler must
/// route every affected variant through the exact same usage gate as a direct permanent delete —
/// never a shortcut, and never partial: if any affected variant is still protected, nothing is
/// mutated at all.
/// </summary>
internal sealed class DeleteProductOptionGroupCommandHandler : IRequestHandler<DeleteProductOptionGroupCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly IInventoryEngine _engine;
    private readonly ICommerceVariantUsageProvider _commerceUsage;

    public DeleteProductOptionGroupCommandHandler(
        ICatalogDbContext db,
        IInventoryEngine engine,
        ICommerceVariantUsageProvider commerceUsage)
    {
        _db = db;
        _engine = engine;
        _commerceUsage = commerceUsage;
    }

    public async Task<Result> Handle(DeleteProductOptionGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.ProductOptionGroups
            .Include(g => g.Options)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        if (group is null)
        {
            return Result.Failure(CatalogErrors.OptionGroupNotFound);
        }

        var directOptionIds = group.Options.Select(o => o.Id).ToList();
        var subtreeOptionIds = new List<Guid>(directOptionIds);
        foreach (var optionId in directOptionIds)
        {
            subtreeOptionIds.AddRange(await OptionGroupSubtreeHelper.CollectDescendantOptionIdsAsync(_db, optionId, cancellationToken));
        }

        if (subtreeOptionIds.Count > 0)
        {
            var affectedVariantIds = await _db.ProductVariantOptions
                .Where(vo => subtreeOptionIds.Contains(vo.OptionId))
                .Join(_db.ProductVariants.Where(v => !v.IsDeleted), vo => vo.VariantId, v => v.Id, (vo, v) => v.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (affectedVariantIds.Count > 0)
            {
                if (!request.Force)
                {
                    return Result.Failure(CatalogErrors.OptionGroupInUse);
                }

                // Pass 1: check every affected variant before touching any of them. Anything less
                // than zero SafeToRemove/ProtectedHistory blocks the whole operation, same as a
                // direct DeleteProductVariantCommand would.
                foreach (var variantId in affectedVariantIds)
                {
                    var usage = await VariantUsageCalculator.ComputeAsync(_db, _commerceUsage, variantId, cancellationToken);
                    if (usage.ProtectedHistory.TotalCount > 0 || usage.SafeToRemove.TotalCount > 0)
                    {
                        return Result.Failure(CatalogErrors.VariantHasProtectedUsage);
                    }
                }

                // Pass 2: every affected variant already proved safe — permanently delete each one
                // through the same engine method (own transaction + row lock + re-check) the
                // single-variant endpoint uses.
                foreach (var variantId in affectedVariantIds)
                {
                    await _engine.DeleteVariantPermanentlyAsync(variantId, cancellationToken);
                }
            }
        }

        foreach (var optionId in directOptionIds)
        {
            await OptionGroupSubtreeHelper.DeleteOptionDescendantsAsync(_db, optionId, cancellationToken);
        }

        _db.ProductOptions.RemoveRange(group.Options);
        _db.ProductOptionGroups.Remove(group);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ReorderProductOptionGroupsCommandHandler : IRequestHandler<ReorderProductOptionGroupsCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public ReorderProductOptionGroupsCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(ReorderProductOptionGroupsCommand request, CancellationToken cancellationToken)
    {
        // Only root groups (ParentOptionId == null) are reordered against each other here; a nested
        // child group's sibling scope is the option it's parented under, not the whole product.
        var groups = await _db.ProductOptionGroups
            .Where(g => g.ProductId == request.ProductId && g.ParentOptionId == null)
            .ToListAsync(cancellationToken);

        if (groups.Count != request.OrderedGroupIds.Count)
        {
            return Result.Failure(CatalogErrors.OptionGroupNotFound);
        }

        for (var index = 0; index < request.OrderedGroupIds.Count; index++)
        {
            var group = groups.FirstOrDefault(g => g.Id == request.OrderedGroupIds[index]);
            if (group is null)
            {
                return Result.Failure(CatalogErrors.OptionGroupNotFound);
            }

            group.Update(group.DisplayName, index, group.IsRequired);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class UpdateProductOptionCommandHandler : IRequestHandler<UpdateProductOptionCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public UpdateProductOptionCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateProductOptionCommand request, CancellationToken cancellationToken)
    {
        var option = await _db.ProductOptions.FirstOrDefaultAsync(o => o.Id == request.OptionId, cancellationToken);
        if (option is null)
        {
            return Result.Failure(CatalogErrors.OptionNotFound);
        }

        option.Update(request.Label, request.SortOrder);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DeleteProductOptionCommandHandler : IRequestHandler<DeleteProductOptionCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public DeleteProductOptionCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteProductOptionCommand request, CancellationToken cancellationToken)
    {
        var option = await _db.ProductOptions.FirstOrDefaultAsync(o => o.Id == request.OptionId, cancellationToken);
        if (option is null)
        {
            return Result.Failure(CatalogErrors.OptionNotFound);
        }

        var descendantOptionIds = await OptionGroupSubtreeHelper.CollectDescendantOptionIdsAsync(_db, option.Id, cancellationToken);
        var subtreeOptionIds = descendantOptionIds.Append(option.Id).ToList();

        var inUse = await _db.ProductVariantOptions.AnyAsync(vo => subtreeOptionIds.Contains(vo.OptionId), cancellationToken);
        if (inUse)
        {
            return Result.Failure(CatalogErrors.OptionInUse);
        }

        await OptionGroupSubtreeHelper.DeleteOptionDescendantsAsync(_db, option.Id, cancellationToken);
        _db.ProductOptions.Remove(option);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ReorderProductOptionsCommandHandler : IRequestHandler<ReorderProductOptionsCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public ReorderProductOptionsCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(ReorderProductOptionsCommand request, CancellationToken cancellationToken)
    {
        var options = await _db.ProductOptions
            .Where(o => o.OptionGroupId == request.GroupId)
            .ToListAsync(cancellationToken);

        if (options.Count != request.OrderedOptionIds.Count)
        {
            return Result.Failure(CatalogErrors.OptionNotFound);
        }

        for (var index = 0; index < request.OrderedOptionIds.Count; index++)
        {
            var option = options.FirstOrDefault(o => o.Id == request.OrderedOptionIds[index]);
            if (option is null)
            {
                return Result.Failure(CatalogErrors.OptionNotFound);
            }

            option.Update(option.Label, index);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DisableInventoryCodeCommandHandler : IRequestHandler<DisableInventoryCodeCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DisableInventoryCodeCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DisableInventoryCodeCommand request, CancellationToken cancellationToken)
    {
        var code = await _db.DigitalInventoryCodes.FirstOrDefaultAsync(c => c.Id == request.CodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure(CatalogErrors.CodeNotFound);
        }

        if (code.Status is InventoryCodeStatus.Sold or InventoryCodeStatus.Reserved)
        {
            return Result.Failure(CatalogErrors.InvalidCodeStatus);
        }

        code.Disable();
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.InventoryAdjusted,
            variantId: code.VariantId,
            codeId: code.Id,
            performedByUserId: _currentUser.UserId,
            details: "Disabled code"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class EnableInventoryCodeCommandHandler : IRequestHandler<EnableInventoryCodeCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public EnableInventoryCodeCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(EnableInventoryCodeCommand request, CancellationToken cancellationToken)
    {
        var code = await _db.DigitalInventoryCodes.FirstOrDefaultAsync(c => c.Id == request.CodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure(CatalogErrors.CodeNotFound);
        }

        code.Enable();
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.InventoryAdjusted,
            variantId: code.VariantId,
            codeId: code.Id,
            performedByUserId: _currentUser.UserId,
            details: "Enabled code"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RevealInventoryCodeCommandHandler : IRequestHandler<RevealInventoryCodeCommand, Result<RevealInventoryCodeDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RevealInventoryCodeCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<RevealInventoryCodeDto>> Handle(RevealInventoryCodeCommand request, CancellationToken cancellationToken)
    {
        // Permission is enforced at the endpoint layer (RequirePermission). Audit is written and
        // persisted before plaintext is ever returned to the caller.
        var code = await _db.DigitalInventoryCodes
            .FirstOrDefaultAsync(c => c.Id == request.CodeId, cancellationToken);

        if (code is null)
        {
            return Result.Failure<RevealInventoryCodeDto>(CatalogErrors.CodeNotFound);
        }

        var productId = await _db.ProductVariants
            .Where(v => v.Id == code.VariantId)
            .Select(v => (Guid?)v.ProductId)
            .FirstOrDefaultAsync(cancellationToken);

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.CodeRevealed,
            productId: productId,
            variantId: code.VariantId,
            codeId: code.Id,
            performedByUserId: _currentUser.UserId,
            details: "Administrator revealed a digital inventory code.",
            orderId: code.OrderId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new RevealInventoryCodeDto(code.DigitalCode));
    }
}

internal sealed class DeleteInventoryCodeCommandHandler : IRequestHandler<DeleteInventoryCodeCommand, Result>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteInventoryCodeCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteInventoryCodeCommand request, CancellationToken cancellationToken)
    {
        var code = await _db.DigitalInventoryCodes.FirstOrDefaultAsync(c => c.Id == request.CodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure(CatalogErrors.CodeNotFound);
        }

        if (code.Status is not (InventoryCodeStatus.Available or InventoryCodeStatus.Disabled))
        {
            return Result.Failure(CatalogErrors.InvalidCodeStatus);
        }

        _db.DigitalInventoryCodes.Remove(code);
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.InventoryAdjusted,
            variantId: code.VariantId,
            codeId: code.Id,
            performedByUserId: _currentUser.UserId,
            details: "Deleted code"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class BulkDisableInventoryCodesCommandHandler : IRequestHandler<BulkDisableInventoryCodesCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BulkDisableInventoryCodesCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(BulkDisableInventoryCodesCommand request, CancellationToken cancellationToken)
    {
        var codes = await _db.DigitalInventoryCodes
            .Where(c => c.VariantId == request.VariantId && request.CodeIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var code in codes)
        {
            if (code.Status is InventoryCodeStatus.Available)
            {
                code.Disable();
                count++;
            }
        }

        if (count > 0)
        {
            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.InventoryAdjusted,
                variantId: request.VariantId,
                performedByUserId: _currentUser.UserId,
                details: $"Bulk disabled {count} codes"));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(count);
    }
}

internal sealed class BulkDeleteInventoryCodesCommandHandler : IRequestHandler<BulkDeleteInventoryCodesCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BulkDeleteInventoryCodesCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(BulkDeleteInventoryCodesCommand request, CancellationToken cancellationToken)
    {
        var codes = await _db.DigitalInventoryCodes
            .Where(c => c.VariantId == request.VariantId && request.CodeIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var removable = codes.Where(c => c.Status is InventoryCodeStatus.Available or InventoryCodeStatus.Disabled).ToList();
        _db.DigitalInventoryCodes.RemoveRange(removable);

        if (removable.Count > 0)
        {
            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.InventoryAdjusted,
                variantId: request.VariantId,
                performedByUserId: _currentUser.UserId,
                details: $"Bulk deleted {removable.Count} codes"));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(removable.Count);
    }
}

internal sealed class ExportInventoryCodesQueryHandler : IRequestHandler<ExportInventoryCodesQuery, Result<ExportInventoryCodesDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ExportInventoryCodesQueryHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<ExportInventoryCodesDto>> Handle(ExportInventoryCodesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.DigitalInventoryCodes.AsNoTracking().Where(c => c.VariantId == request.VariantId);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<InventoryCodeStatus>(request.Status, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        // Codes are encrypted at rest, so ordering by the raw column would sort by ciphertext.
        var codes = await query.OrderBy(c => c.CreatedOnUtc).ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Code,Status,SerialNumber,ExpirationDate");

        foreach (var code in codes)
        {
            // Exports never expose plaintext — only the dedicated reveal endpoint may.
            var maskedCode = InventoryCodeMasking.Mask(code.DigitalCode);
            var maskedSerial = code.SerialNumber is null ? string.Empty : InventoryCodeMasking.Mask(code.SerialNumber);
            builder.AppendLine($"{EscapeCsv(maskedCode)},{code.Status},{EscapeCsv(maskedSerial)},{code.ExpirationDate:O}");
        }

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.CodeExported,
            variantId: request.VariantId,
            performedByUserId: _currentUser.UserId,
            details: $"Exported {codes.Count} codes"));

        await _db.SaveChangesAsync(cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Result.Success(new ExportInventoryCodesDto(
            $"variant-{request.VariantId}-codes.csv",
            "text/csv",
            bytes));
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

internal sealed class BulkDeleteProductVariantsCommandHandler : IRequestHandler<BulkDeleteProductVariantsCommand, Result<BulkVariantsResultDto>>
{
    private readonly ISender _sender;

    public BulkDeleteProductVariantsCommandHandler(ISender sender) => _sender = sender;

    public async Task<Result<BulkVariantsResultDto>> Handle(BulkDeleteProductVariantsCommand request, CancellationToken cancellationToken)
    {
        if (request.VariantIds.Count == 0)
        {
            return Result.Failure<BulkVariantsResultDto>(CatalogErrors.VariantBulkEmpty);
        }

        var success = 0;
        var errors = new List<string>();
        var blockedVariantIds = new List<Guid>();

        foreach (var variantId in request.VariantIds.Distinct())
        {
            var result = await _sender.Send(new DeleteProductVariantCommand(variantId), cancellationToken);
            if (result.IsSuccess)
            {
                success++;
            }
            else
            {
                errors.Add($"{variantId}: {result.Error.Description}");
                blockedVariantIds.Add(variantId);
            }
        }

        return Result.Success(new BulkVariantsResultDto(success, errors.Count, errors, blockedVariantIds));
    }
}

internal sealed class BulkDuplicateProductVariantsCommandHandler : IRequestHandler<BulkDuplicateProductVariantsCommand, Result<BulkVariantsResultDto>>
{
    private readonly ISender _sender;

    public BulkDuplicateProductVariantsCommandHandler(ISender sender) => _sender = sender;

    public async Task<Result<BulkVariantsResultDto>> Handle(BulkDuplicateProductVariantsCommand request, CancellationToken cancellationToken)
    {
        if (request.VariantIds.Count == 0)
        {
            return Result.Failure<BulkVariantsResultDto>(CatalogErrors.VariantBulkEmpty);
        }

        var success = 0;
        var errors = new List<string>();

        foreach (var variantId in request.VariantIds.Distinct())
        {
            var result = await _sender.Send(new DuplicateProductVariantCommand(variantId, null), cancellationToken);
            if (result.IsSuccess)
            {
                success++;
            }
            else
            {
                errors.Add($"{variantId}: {result.Error.Description}");
            }
        }

        return Result.Success(new BulkVariantsResultDto(success, errors.Count, errors, []));
    }
}

internal sealed class ExportVariantsInventoryCodesQueryHandler : IRequestHandler<ExportVariantsInventoryCodesQuery, Result<ExportInventoryCodesDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ExportVariantsInventoryCodesQueryHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<ExportInventoryCodesDto>> Handle(ExportVariantsInventoryCodesQuery request, CancellationToken cancellationToken)
    {
        if (request.VariantIds.Count == 0)
        {
            return Result.Failure<ExportInventoryCodesDto>(CatalogErrors.VariantBulkEmpty);
        }

        var skuByVariantId = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == request.ProductId && request.VariantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Sku })
            .ToDictionaryAsync(v => v.Id, v => v.Sku, cancellationToken);

        var query = _db.DigitalInventoryCodes.AsNoTracking().Where(c => request.VariantIds.Contains(c.VariantId));

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<InventoryCodeStatus>(request.Status, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        // Codes are encrypted at rest, so ordering by the raw column would sort by ciphertext.
        var codes = await query.OrderBy(c => c.CreatedOnUtc).ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Variant,Code,Status,SerialNumber,ExpirationDate");

        foreach (var code in codes)
        {
            var maskedCode = InventoryCodeMasking.Mask(code.DigitalCode);
            var maskedSerial = code.SerialNumber is null ? string.Empty : InventoryCodeMasking.Mask(code.SerialNumber);
            var sku = skuByVariantId.GetValueOrDefault(code.VariantId, code.VariantId.ToString());
            builder.AppendLine($"{EscapeCsv(sku)},{EscapeCsv(maskedCode)},{code.Status},{EscapeCsv(maskedSerial)},{code.ExpirationDate:O}");
        }

        foreach (var variantId in request.VariantIds.Distinct())
        {
            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.CodeExported,
                variantId: variantId,
                performedByUserId: _currentUser.UserId,
                details: $"Exported codes as part of a {request.VariantIds.Count}-variant bulk export"));
        }

        await _db.SaveChangesAsync(cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Result.Success(new ExportInventoryCodesDto(
            $"product-{request.ProductId}-variants-codes.csv",
            "text/csv",
            bytes));
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
