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

public sealed record GetSuppliersQuery(int PageNumber, int PageSize, string? SearchTerm) : IRequest<Result<IReadOnlyList<InventorySupplierDto>>>;

internal sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, Result<IReadOnlyList<InventorySupplierDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetSuppliersQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<InventorySupplierDto>>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.InventorySuppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(s => s.CompanyName.Contains(request.SearchTerm));
        }

        var items = await query
            .OrderBy(s => s.CompanyName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new InventorySupplierDto(
                s.Id, s.CompanyName, s.ContactPerson, s.Email, s.Phone, s.Website,
                s.Country, s.Currency, s.Notes, s.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InventorySupplierDto>>(items);
    }
}

public sealed record CreateSupplierCommand(
    string CompanyName,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Website,
    string? Country,
    string Currency,
    string? Notes) : IRequest<Result<Guid>>;

internal sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateSupplierCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = InventorySupplier.Create(
            request.CompanyName,
            request.ContactPerson,
            request.Email,
            request.Phone,
            request.Website,
            request.Country,
            request.Currency,
            request.Notes);

        _db.InventorySuppliers.Add(supplier);
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.SupplierAdded,
            supplierId: supplier.Id,
            performedByUserId: _currentUser.UserId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(supplier.Id);
    }
}

public sealed record CreateInventoryBatchCommand(
    Guid VariantId,
    string Name,
    Guid? SupplierId,
    DateTimeOffset? PurchaseDate,
    string Currency,
    decimal PurchaseCost,
    decimal? ExpectedMargin,
    string? Notes) : IRequest<Result<Guid>>;

internal sealed class CreateInventoryBatchCommandHandler : IRequestHandler<CreateInventoryBatchCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateInventoryBatchCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateInventoryBatchCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken);
        if (variant is null)
        {
            return Result.Failure<Guid>(CatalogErrors.VariantNotFound);
        }

        var batch = InventoryBatch.Create(
            request.VariantId,
            request.Name,
            request.SupplierId,
            request.PurchaseDate,
            request.Currency,
            request.PurchaseCost,
            request.ExpectedMargin,
            request.Notes);

        _db.InventoryBatches.Add(batch);
        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.BatchCreated,
            productId: variant.ProductId,
            variantId: variant.Id,
            batchId: batch.Id,
            performedByUserId: _currentUser.UserId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(batch.Id);
    }
}

public sealed record GetInventoryBatchesQuery(Guid VariantId) : IRequest<Result<IReadOnlyList<InventoryBatchDto>>>;

internal sealed class GetInventoryBatchesQueryHandler : IRequestHandler<GetInventoryBatchesQuery, Result<IReadOnlyList<InventoryBatchDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetInventoryBatchesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<InventoryBatchDto>>> Handle(GetInventoryBatchesQuery request, CancellationToken cancellationToken)
    {
        var batches = await _db.InventoryBatches
            .AsNoTracking()
            .Where(b => b.VariantId == request.VariantId)
            .OrderByDescending(b => b.ImportDate)
            .Select(b => new InventoryBatchDto(
                b.Id, b.VariantId, b.SupplierId, b.Name, b.PurchaseDate, b.ImportDate,
                b.Currency, b.PurchaseCost, b.ExpectedMargin, b.Notes,
                b.TotalCodes, b.AvailableCodes, b.ReservedCodes, b.SoldCodes, b.ReturnedCodes, b.ExpiredCodes))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InventoryBatchDto>>(batches);
    }
}

public sealed record ImportInventoryCodesCommand(
    Guid VariantId,
    Guid BatchId,
    IReadOnlyList<string> Codes) : IRequest<Result<ImportCodesResultDto>>;

public sealed record ImportCodesResultDto(int Imported, int Duplicates, int Invalid);

internal sealed class ImportInventoryCodesCommandHandler : IRequestHandler<ImportInventoryCodesCommand, Result<ImportCodesResultDto>>
{
    private readonly IInventoryEngine _engine;
    private readonly ICurrentUserService _currentUser;

    public ImportInventoryCodesCommandHandler(IInventoryEngine engine, ICurrentUserService currentUser)
    {
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<ImportCodesResultDto>> Handle(ImportInventoryCodesCommand request, CancellationToken cancellationToken)
    {
        var items = request.Codes
            .Select(c => new ImportCodeItem(c))
            .ToList();

        try
        {
            var result = await _engine.ImportCodesAsync(
                request.VariantId,
                request.BatchId,
                items,
                _currentUser.UserId,
                cancellationToken);

            return Result.Success(new ImportCodesResultDto(result.Imported, result.Duplicates, result.Invalid));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Batch not found.")
        {
            return Result.Failure<ImportCodesResultDto>(CatalogErrors.BatchNotFound);
        }
    }
}

public sealed record GetInventoryCodesQuery(
    Guid VariantId,
    int PageNumber,
    int PageSize,
    string? Status,
    string? SearchTerm) : IRequest<Result<IReadOnlyList<DigitalInventoryCodeDto>>>;

internal sealed class GetInventoryCodesQueryHandler : IRequestHandler<GetInventoryCodesQuery, Result<IReadOnlyList<DigitalInventoryCodeDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetInventoryCodesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<DigitalInventoryCodeDto>>> Handle(GetInventoryCodesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.DigitalInventoryCodes.AsNoTracking().Where(c => c.VariantId == request.VariantId);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<InventoryCodeStatus>(request.Status, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // Codes are encrypted at rest (non-deterministic ciphertext), so substring matching on the
            // raw column no longer works. CodeHash already exists for exact-match duplicate detection;
            // reuse it here instead of adding new crypto/search machinery.
            var searchHash = DigitalInventoryCode.ComputeHash(request.SearchTerm);
            query = query.Where(c => c.CodeHash == searchHash);
        }

        var items = await query
            .OrderByDescending(c => c.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new DigitalInventoryCodeDto(
                c.Id, c.VariantId, c.BatchId, c.SupplierId, c.DigitalCode, c.SerialNumber,
                c.Status.ToString(), c.PurchaseCost, c.SellingPriceOverride, c.Currency,
                c.ExpirationDate, c.ReservedOnUtc, c.SoldOnUtc))
            .ToListAsync(cancellationToken);

        // Grid never exposes plaintext — only the dedicated reveal endpoint may.
        var masked = items
            .Select(c => c with { DigitalCode = InventoryCodeMasking.Mask(c.DigitalCode), SerialNumber = c.SerialNumber is null ? null : InventoryCodeMasking.Mask(c.SerialNumber) })
            .ToList();

        return Result.Success<IReadOnlyList<DigitalInventoryCodeDto>>(masked);
    }
}

public sealed record GetInventoryAuditLogsQuery(Guid? VariantId, int PageNumber, int PageSize) : IRequest<Result<IReadOnlyList<InventoryAuditLogDto>>>;

internal sealed class GetInventoryAuditLogsQueryHandler : IRequestHandler<GetInventoryAuditLogsQuery, Result<IReadOnlyList<InventoryAuditLogDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetInventoryAuditLogsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<InventoryAuditLogDto>>> Handle(GetInventoryAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.InventoryAuditLogs.AsNoTracking().AsQueryable();
        if (request.VariantId is not null)
        {
            query = query.Where(l => l.VariantId == request.VariantId);
        }

        var items = await query
            .OrderByDescending(l => l.OccurredOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new InventoryAuditLogDto(
                l.Id, l.Action.ToString(), l.ProductId, l.VariantId, l.BatchId, l.CodeId, l.Details, l.OccurredOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InventoryAuditLogDto>>(items);
    }
}

public sealed record GetActiveReservationsQuery(int PageNumber, int PageSize) : IRequest<Result<IReadOnlyList<InventoryReservationDto>>>;

internal sealed class GetActiveReservationsQueryHandler : IRequestHandler<GetActiveReservationsQuery, Result<IReadOnlyList<InventoryReservationDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetActiveReservationsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<InventoryReservationDto>>> Handle(GetActiveReservationsQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.InventoryReservations
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.ExpiresOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new InventoryReservationDto(r.Id, r.CodeId, r.VariantId, r.UserId, r.CartId, r.ExpiresOnUtc, r.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InventoryReservationDto>>(items);
    }
}

public sealed record ReleaseInventoryReservationCommand(Guid CodeId) : IRequest<Result>;

internal sealed class ReleaseInventoryReservationCommandHandler : IRequestHandler<ReleaseInventoryReservationCommand, Result>
{
    private readonly IInventoryEngine _engine;

    public ReleaseInventoryReservationCommandHandler(IInventoryEngine engine) => _engine = engine;

    public async Task<Result> Handle(ReleaseInventoryReservationCommand request, CancellationToken cancellationToken)
    {
        await _engine.ReleaseReservationsAsync([request.CodeId], cancellationToken);
        return Result.Success();
    }
}
