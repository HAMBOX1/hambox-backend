using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Infrastructure.Services;

internal sealed class InventoryEngine : IInventoryEngine
{
    private readonly ICatalogDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformSettingsProvider _platformSettings;

    public InventoryEngine(
        ICatalogDbContext db,
        ICurrentUserService currentUser,
        IPlatformSettingsProvider platformSettings)
    {
        _db = db;
        _currentUser = currentUser;
        _platformSettings = platformSettings;
    }

    public async Task<VariantStockSnapshot> GetVariantStockAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var bulk = await GetVariantStockBulkAsync([variantId], cancellationToken);
        return bulk.GetValueOrDefault(variantId) ?? EmptySnapshot(variantId);
    }

    public async Task<IReadOnlyDictionary<Guid, VariantStockSnapshot>> GetVariantStockBulkAsync(
        IEnumerable<Guid> variantIds,
        CancellationToken cancellationToken = default)
    {
        var ids = variantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, VariantStockSnapshot>();
        }

        await ExpireStaleReservationsAsync(cancellationToken);

        var inventorySettings = await _platformSettings.GetInventoryAsync(cancellationToken);

        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.LowStockThreshold })
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var counts = await _db.DigitalInventoryCodes
            .AsNoTracking()
            .Where(c => ids.Contains(c.VariantId))
            .GroupBy(c => new { c.VariantId, c.Status })
            .Select(g => new { g.Key.VariantId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, VariantStockSnapshot>();
        foreach (var id in ids)
        {
            var threshold = variants.TryGetValue(id, out var v) && v.LowStockThreshold > 0
                ? v.LowStockThreshold
                : inventorySettings.LowStockThreshold;
            var available = counts.Where(c => c.VariantId == id && c.Status == InventoryCodeStatus.Available).Sum(c => c.Count);
            var reserved = counts.Where(c => c.VariantId == id && c.Status == InventoryCodeStatus.Reserved).Sum(c => c.Count);
            var sold = counts.Where(c => c.VariantId == id && c.Status == InventoryCodeStatus.Sold).Sum(c => c.Count);
            var expired = counts.Where(c => c.VariantId == id && c.Status == InventoryCodeStatus.Expired).Sum(c => c.Count);
            var disabled = counts.Where(c => c.VariantId == id && c.Status == InventoryCodeStatus.Disabled).Sum(c => c.Count);

            result[id] = new VariantStockSnapshot(
                id,
                available,
                reserved,
                sold,
                expired,
                disabled,
                available > 0 && available <= threshold,
                available <= 0);
        }

        return result;
    }

    public Task<bool> ProductHasVariantsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _db.ProductVariants.AnyAsync(
            v => v.ProductId == productId
                && !v.IsDeleted
                && v.Status == ProductVariantStatus.Active
                && v.IsVisible,
            cancellationToken);

    public async Task<IReadOnlyList<ReservedCodeSnapshot>> ReserveCodesAsync(
        Guid variantId,
        int quantity,
        string? userId,
        Guid? cartId,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        await ExpireStaleReservationsAsync(cancellationToken);

        var lockedIds = await GetLockedAvailableCodeIdsAsync(variantId, quantity, cancellationToken);
        if (lockedIds.Count < quantity)
        {
            throw new InvalidOperationException("Insufficient inventory codes.");
        }

        var codes = await _db.DigitalInventoryCodes
            .Where(c => lockedIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var inventorySettings = await _platformSettings.GetInventoryAsync(cancellationToken);

        var reserved = new List<ReservedCodeSnapshot>();
        foreach (var code in codes)
        {
            var previous = code.Status;
            code.Reserve();
            var reservation = InventoryReservation.Create(
                code.Id,
                variantId,
                userId,
                cartId,
                inventorySettings.ReservationTimeoutMinutes);
            _db.InventoryReservations.Add(reservation);

            var batch = await _db.InventoryBatches.FirstAsync(b => b.Id == code.BatchId, cancellationToken);
            batch.OnCodeStatusChanged(previous, InventoryCodeStatus.Reserved);

            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.CodeReserved,
                variantId: variantId,
                batchId: code.BatchId,
                codeId: code.Id,
                performedByUserId: userId ?? _currentUser.UserId,
                details: $"Reserved until {reservation.ExpiresOnUtc:O}"));

            reserved.Add(new ReservedCodeSnapshot(code.Id, variantId, code.DigitalCode, reservation.ExpiresOnUtc));
        }

        await PersistIfNotEnlistedAsync(cancellationToken);
        return reserved;
    }

    public async Task ReleaseReservationsForCartAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var active = await _db.InventoryReservations
            .Where(r => r.CartId == cartId && r.IsActive)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            return;
        }

        await ReleaseReservationRecordsAsync(active, cancellationToken);
    }

    public async Task ReleaseReservationsAsync(IEnumerable<Guid> codeIds, CancellationToken cancellationToken = default)
    {
        var ids = codeIds.Distinct().ToList();
        var active = await _db.InventoryReservations
            .Where(r => r.IsActive && ids.Contains(r.CodeId))
            .ToListAsync(cancellationToken);

        await ReleaseReservationRecordsAsync(active, cancellationToken);
    }

    public async Task<IReadOnlyList<CommittedCodeSnapshot>> CommitReservationsAsync(
        Guid orderId,
        IReadOnlyList<(Guid OrderItemId, Guid CodeId)> assignments,
        CancellationToken cancellationToken = default)
    {
        var committed = new List<CommittedCodeSnapshot>();

        foreach (var (orderItemId, codeId) in assignments)
        {
            var code = await FindTrackedCodeAsync(codeId, cancellationToken);
            var previous = code.Status;
            code.MarkSold(orderId, orderItemId);

            var reservation = await _db.InventoryReservations
                .FirstOrDefaultAsync(r => r.CodeId == codeId && r.IsActive, cancellationToken);
            reservation?.Complete();

            var batch = await _db.InventoryBatches.FirstAsync(b => b.Id == code.BatchId, cancellationToken);
            batch.OnCodeStatusChanged(previous, InventoryCodeStatus.Sold);

            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.CodeSold,
                variantId: code.VariantId,
                batchId: code.BatchId,
                codeId: code.Id,
                performedByUserId: _currentUser.UserId,
                details: $"Sold on order {orderId}"));

            committed.Add(new CommittedCodeSnapshot(code.Id, orderItemId, code.DigitalCode));
        }

        await PersistIfNotEnlistedAsync(cancellationToken);
        return committed;
    }

    public async Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default)
    {
        var stale = await _db.InventoryReservations
            .Where(r => r.IsActive && r.ExpiresOnUtc <= DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        await ReleaseReservationRecordsAsync(stale, cancellationToken);
        return stale.Count;
    }

    public async Task<InventoryStatisticsSnapshot> GetStatisticsAsync(
        Guid? productId = null,
        Guid? variantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.DigitalInventoryCodes.AsNoTracking().AsQueryable();

        if (variantId is not null)
        {
            query = query.Where(c => c.VariantId == variantId);
        }
        else if (productId is not null)
        {
            var variantIds = _db.ProductVariants.Where(v => v.ProductId == productId).Select(v => v.Id);
            query = query.Where(c => variantIds.Contains(c.VariantId));
        }

        var codes = await query.Select(c => new { c.Status, c.PurchaseCost, c.SellingPriceOverride }).ToListAsync(cancellationToken);

        var available = codes.Count(c => c.Status == InventoryCodeStatus.Available);
        var reserved = codes.Count(c => c.Status == InventoryCodeStatus.Reserved);
        var sold = codes.Count(c => c.Status == InventoryCodeStatus.Sold);
        var expired = codes.Count(c => c.Status == InventoryCodeStatus.Expired);

        var purchaseCost = codes.Where(c => c.PurchaseCost.HasValue).Sum(c => c.PurchaseCost!.Value);
        var inventoryValue = codes
            .Where(c => c.Status is InventoryCodeStatus.Available or InventoryCodeStatus.Reserved)
            .Sum(c => c.SellingPriceOverride ?? c.PurchaseCost ?? 0);

        var variantQuery = _db.ProductVariants.AsNoTracking().AsQueryable();
        if (productId is not null)
        {
            variantQuery = variantQuery.Where(v => v.ProductId == productId);
        }

        if (variantId is not null)
        {
            variantQuery = variantQuery.Where(v => v.Id == variantId);
        }

        var variantIdsList = await variantQuery.Select(v => v.Id).ToListAsync(cancellationToken);
        var stockMap = await GetVariantStockBulkAsync(variantIdsList, cancellationToken);

        return new InventoryStatisticsSnapshot(
            available,
            reserved,
            sold,
            expired,
            stockMap.Values.Count(s => s.IsLowStock),
            stockMap.Values.Count(s => s.IsOutOfStock),
            inventoryValue,
            purchaseCost,
            inventoryValue * 1.2m,
            inventoryValue * 1.2m - purchaseCost);
    }

    public async Task<ImportCodesResult> ImportCodesAsync(
        Guid variantId,
        Guid batchId,
        IReadOnlyList<ImportCodeItem> codes,
        string? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.InventoryBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.VariantId == variantId, cancellationToken)
            ?? throw new InvalidOperationException("Batch not found.");

        var targetVariant = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new { v.Sku, v.ProductId })
            .FirstAsync(cancellationToken);

        var targetProductName = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == targetVariant.ProductId)
            .Select(p => p.NameEn)
            .FirstAsync(cancellationToken);

        var totalSubmitted = codes.Count;

        var normalized = codes
            .Select(item => (
                Item: item,
                Hash: string.IsNullOrWhiteSpace(item.DigitalCode) ? null : DigitalInventoryCode.ComputeHash(item.DigitalCode)))
            .ToList();

        var candidateHashes = normalized
            .Where(n => n.Hash is not null)
            .Select(n => n.Hash!)
            .Distinct()
            .ToList();

        // Indexed lookup against the unique CodeHash index — only the submitted hashes are ever
        // pulled back, never the whole table, so this stays cheap regardless of catalog size.
        var existingRows = await _db.DigitalInventoryCodes
            .AsNoTracking()
            .Where(c => candidateHashes.Contains(c.CodeHash))
            .Select(c => new { c.CodeHash, c.VariantId, c.BatchId })
            .ToListAsync(cancellationToken);

        var locationByHash = existingRows.ToDictionary(r => r.CodeHash, r => (VariantId: r.VariantId, BatchId: r.BatchId));

        // Batch-resolve the product/variant/batch names for every *other* location a duplicate
        // already lives in, so "where does this already exist" never costs an N+1 query.
        var foreignVariantIds = locationByHash.Values.Select(v => v.VariantId).Where(id => id != variantId).Distinct().ToList();
        var foreignBatchIds = locationByHash.Values.Select(v => v.BatchId).Where(id => id != batchId).Distinct().ToList();

        var variantLookup = foreignVariantIds.Count == 0
            ? new Dictionary<Guid, (string Sku, string ProductName)>()
            : await _db.ProductVariants
                .AsNoTracking()
                .Where(v => foreignVariantIds.Contains(v.Id))
                .Join(_db.Products.AsNoTracking(), v => v.ProductId, p => p.Id, (v, p) => new { v.Id, v.Sku, ProductName = p.NameEn })
                .ToDictionaryAsync(v => v.Id, v => (v.Sku, v.ProductName), cancellationToken);

        var batchLookup = foreignBatchIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.InventoryBatches
                .AsNoTracking()
                .Where(b => foreignBatchIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Name })
                .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        (string ProductName, string VariantSku, string? BatchName) ResolveLocation(Guid dupVariantId, Guid dupBatchId)
        {
            if (dupVariantId == variantId)
            {
                return (targetProductName, targetVariant.Sku, batch.Name);
            }

            if (variantLookup.TryGetValue(dupVariantId, out var v))
            {
                return (v.ProductName, v.Sku, batchLookup.GetValueOrDefault(dupBatchId));
            }

            return ("Unknown product", "Unknown variant", null);
        }

        var imported = 0;
        var invalid = 0;
        var duplicateDetails = new List<ImportCodeDuplicate>();

        foreach (var (item, hash) in normalized)
        {
            if (hash is null)
            {
                invalid++;
                continue;
            }

            if (locationByHash.TryGetValue(hash, out var location))
            {
                var (productName, variantSku, batchName) = ResolveLocation(location.VariantId, location.BatchId);
                duplicateDetails.Add(new ImportCodeDuplicate(item.DigitalCode.Trim(), productName, variantSku, batchName));
                continue;
            }

            var code = DigitalInventoryCode.Create(
                variantId,
                batchId,
                item.DigitalCode,
                batch.SupplierId,
                item.SerialNumber,
                item.Pin,
                item.PurchaseCost ?? batch.PurchaseCost,
                null,
                batch.Currency,
                null,
                item.ExpirationDate);

            _db.DigitalInventoryCodes.Add(code);
            // A later repeat of this same code within the same submission should also report as a
            // duplicate, pointing back here rather than silently importing twice.
            locationByHash[hash] = (variantId, batchId);
            imported++;
        }

        var duplicates = duplicateDetails.Count;

        if (imported > 0)
        {
            batch.RecordImport(imported);
            _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.CodeImported,
                variantId: variantId,
                batchId: batchId,
                performedByUserId: performedByUserId ?? _currentUser.UserId,
                details: $"Imported {imported} of {totalSubmitted} codes ({duplicates} duplicates, {invalid} invalid)"));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ImportCodesResult(totalSubmitted, imported, duplicates, invalid, duplicateDetails);
    }

    private async Task ReleaseReservationRecordsAsync(
        IReadOnlyList<InventoryReservation> reservations,
        CancellationToken cancellationToken)
    {
        foreach (var reservation in reservations)
        {
            var code = await FindTrackedCodeAsync(reservation.CodeId, cancellationToken);
            if (code.Status == InventoryCodeStatus.Reserved)
            {
                var previous = code.Status;
                code.Release();
                var batch = await _db.InventoryBatches.FirstAsync(b => b.Id == code.BatchId, cancellationToken);
                batch.OnCodeStatusChanged(previous, InventoryCodeStatus.Available);

                _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                    InventoryAuditAction.CodeReleased,
                    variantId: code.VariantId,
                    batchId: code.BatchId,
                    codeId: code.Id,
                    performedByUserId: _currentUser.UserId));
            }

            reservation.Release();
        }

        await PersistIfNotEnlistedAsync(cancellationToken);
    }

    /// <summary>
    /// Selects candidate code ids for reservation, taking a pessimistic row lock (UPDLOCK, ROWLOCK) so two
    /// concurrent checkouts can never select the same "Available" code. The second transaction's identical
    /// query blocks until the first commits/rolls back, then naturally excludes whatever was just reserved.
    /// </summary>
    private async Task<List<Guid>> GetLockedAvailableCodeIdsAsync(Guid variantId, int quantity, CancellationToken cancellationToken)
    {
        if (_db is not CatalogDbContext catalogDbContext)
        {
            return await _db.DigitalInventoryCodes
                .Where(c => c.VariantId == variantId && c.Status == InventoryCodeStatus.Available)
                .OrderBy(c => c.CreatedOnUtc)
                .Take(quantity)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }

        var statusName = InventoryCodeStatus.Available.ToString();
        return await catalogDbContext.Database
            .SqlQuery<Guid>($@"
                SELECT TOP ({quantity}) Id
                FROM catalog.DigitalInventoryCodes WITH (UPDLOCK, ROWLOCK)
                WHERE VariantId = {variantId} AND Status = {statusName}
                ORDER BY CreatedOnUtc")
            .ToListAsync(cancellationToken);
    }

    private async Task<DigitalInventoryCode> FindTrackedCodeAsync(Guid codeId, CancellationToken cancellationToken)
    {
        if (_db is CatalogDbContext catalogDbContext)
        {
            var tracked = catalogDbContext.DigitalInventoryCodes.Local
                .FirstOrDefault(c => c.Id == codeId);

            if (tracked is not null)
            {
                return tracked;
            }
        }

        return await _db.DigitalInventoryCodes.FirstAsync(c => c.Id == codeId, cancellationToken);
    }

    private Task PersistIfNotEnlistedAsync(CancellationToken cancellationToken) =>
        IsEnlistedInAmbientTransaction()
            ? Task.CompletedTask
            : _db.SaveChangesAsync(cancellationToken);

    private bool IsEnlistedInAmbientTransaction() =>
        _db is CatalogDbContext catalogDbContext && catalogDbContext.Database.CurrentTransaction is not null;

    private static VariantStockSnapshot EmptySnapshot(Guid variantId) =>
        new(variantId, 0, 0, 0, 0, 0, false, true);
}
