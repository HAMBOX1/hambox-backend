using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Lists the archived snapshots of deleted inventory codes. Plaintext secrets are returned, so the
/// caller MUST enforce Owner-only access before sending this query (see the inventory endpoint).
/// </summary>
public sealed record GetDeletedInventoryCodesQuery(Guid? VariantId, int Page = 1, int PageSize = 50)
    : IRequest<Result<DeletedInventoryCodesPageDto>>;

public sealed record DeletedInventoryCodeDto(
    Guid Id,
    Guid OriginalCodeId,
    Guid VariantId,
    Guid BatchId,
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

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(c => c.DeletedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(c => new DeletedInventoryCodeDto(
            c.Id, c.OriginalCodeId, c.VariantId, c.BatchId, c.DigitalCode, c.SerialNumber, c.Pin,
            c.PurchaseCost, c.Currency, c.Notes, c.StatusAtDeletion.ToString(), c.DeletedOnUtc,
            c.DeletedByUserId, c.Reason)).ToList();

        return Result.Success(new DeletedInventoryCodesPageDto(items, total, page, pageSize));
    }
}
