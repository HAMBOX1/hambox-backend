using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Lists the archived snapshots of deleted inventory codes. Plaintext secrets are returned, so the
/// caller MUST enforce Owner-only access before sending this query (see the inventory endpoint).
/// </summary>
public sealed record GetDeletedInventoryCodesQuery(Guid? VariantId, string? SearchTerm = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<DeletedInventoryCodesPageDto>>;

public sealed record DeletedInventoryCodeDto(
    Guid Id,
    Guid OriginalCodeId,
    Guid VariantId,
    Guid BatchId,
    Guid? ProductId,
    string? ProductName,
    string? VariantSku,
    string DigitalCode,
    string? SerialNumber,
    string? Pin,
    decimal? PurchaseCost,
    string Currency,
    string? Notes,
    string StatusAtDeletion,
    DateTimeOffset DeletedOnUtc,
    Guid? DeletedByUserId,
    string Reason);

public sealed record DeletedInventoryCodesPageDto(
    IReadOnlyList<DeletedInventoryCodeDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

internal sealed class GetDeletedInventoryCodesQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetDeletedInventoryCodesQuery, Result<DeletedInventoryCodesPageDto>>
{
    public async Task<Result<DeletedInventoryCodesPageDto>> Handle(
        GetDeletedInventoryCodesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.DeletedInventoryCodes.AsNoTracking();
        if (request.VariantId is { } variantId)
        {
            query = query.Where(c => c.VariantId == variantId);
        }

        // Product/variant lookups include soft-deleted rows: a code deleted along with its product is exactly
        // the kind of record the owner needs to find again.
        var term = request.SearchTerm?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var matchingVariantIds = db.ProductVariants.IgnoreQueryFilters().AsNoTracking()
                .Where(v => v.Sku.Contains(term)
                    || db.Products.IgnoreQueryFilters().Any(p => p.Id == v.ProductId && (p.NameEn.Contains(term) || p.NameAr.Contains(term))))
                .Select(v => v.Id);

            query = query.Where(c => matchingVariantIds.Contains(c.VariantId) || c.Reason.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(c => c.DeletedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var variantIds = rows.Select(r => r.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants.IgnoreQueryFilters().AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.Sku })
            .ToListAsync(cancellationToken);

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameEn })
            .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);

        var variantById = variants.ToDictionary(v => v.Id);

        var items = rows.Select(c =>
        {
            variantById.TryGetValue(c.VariantId, out var variant);
            string? productName = null;
            if (variant is not null)
            {
                productNames.TryGetValue(variant.ProductId, out productName);
            }

            return new DeletedInventoryCodeDto(
                c.Id, c.OriginalCodeId, c.VariantId, c.BatchId, variant?.ProductId, productName, variant?.Sku,
                c.DigitalCode, c.SerialNumber, c.Pin, c.PurchaseCost, c.Currency, c.Notes,
                c.StatusAtDeletion.ToString(), c.DeletedOnUtc, c.DeletedByUserId, c.Reason);
        }).ToList();

        return Result.Success(new DeletedInventoryCodesPageDto(items, total, page, pageSize));
    }
}

/// <summary>
/// Puts an archived code back into live stock as Available on its original variant, then removes it from the
/// archive. Owner-only (the endpoint enforces it). Refuses when the same code value already exists in stock or the
/// variant has no batch to put it in, so a restore can never create a duplicate or an orphan.
/// </summary>
public sealed record RestoreDeletedInventoryCodeCommand(Guid ArchiveId) : IRequest<Result>;

internal sealed class RestoreDeletedInventoryCodeCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RestoreDeletedInventoryCodeCommand, Result>
{
    private static readonly Error NotFound = new("Inventory.DeletedCodeNotFound", "That deleted code was not found.");
    private static readonly Error VariantMissing = new("Inventory.RestoreVariantMissing", "The variant this code belonged to no longer exists, so it cannot be restored.");
    private static readonly Error NoBatch = new("Inventory.RestoreNoBatch", "The variant has no import batch to restore the code into.");
    private static readonly Error AlreadyExists = new("Inventory.RestoreDuplicate", "This code already exists in stock, so it was not restored.");

    public async Task<Result> Handle(RestoreDeletedInventoryCodeCommand request, CancellationToken cancellationToken)
    {
        var archived = await db.DeletedInventoryCodes.FirstOrDefaultAsync(c => c.Id == request.ArchiveId, cancellationToken);
        if (archived is null)
        {
            return Result.Failure(NotFound);
        }

        var variantExists = await db.ProductVariants.AnyAsync(v => v.Id == archived.VariantId, cancellationToken);
        if (!variantExists)
        {
            return Result.Failure(VariantMissing);
        }

        var hash = DigitalInventoryCode.ComputeHash(archived.DigitalCode);
        if (await db.DigitalInventoryCodes.AnyAsync(c => c.CodeHash == hash, cancellationToken))
        {
            return Result.Failure(AlreadyExists);
        }

        var batchId = await db.InventoryBatches
            .Where(b => b.Id == archived.BatchId && b.VariantId == archived.VariantId)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.InventoryBatches
                .Where(b => b.VariantId == archived.VariantId)
                .OrderByDescending(b => b.CreatedOnUtc)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (batchId is null)
        {
            return Result.Failure(NoBatch);
        }

        var restored = DigitalInventoryCode.Create(
            archived.VariantId,
            batchId.Value,
            archived.DigitalCode,
            archived.SupplierId,
            archived.SerialNumber,
            archived.Pin,
            archived.PurchaseCost,
            null,
            archived.Currency,
            archived.Notes,
            archived.ExpirationDate);

        db.DigitalInventoryCodes.Add(restored);
        db.DeletedInventoryCodes.Remove(archived);
        db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.InventoryAdjusted,
            variantId: archived.VariantId,
            codeId: restored.Id,
            performedByUserId: currentUser.UserId,
            details: "Restored deleted code"));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
