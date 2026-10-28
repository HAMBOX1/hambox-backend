using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public sealed record ImageCodeFile(Stream Content, string FileName, string ContentType, long Length);

/// <summary>
/// Adds pictures as stock: each uploaded image becomes ONE code (what the customer receives is that picture).
/// The batch is created by the caller exactly as for text codes.
/// </summary>
public sealed record ImportImageCodesCommand(Guid VariantId, Guid BatchId, IReadOnlyList<ImageCodeFile> Files)
    : IRequest<Result<ImportCodesResultDto>>;

internal sealed class ImportImageCodesCommandHandler(
    ICatalogDbContext db,
    IFileStorage fileStorage,
    ICurrentUserService currentUser) : IRequestHandler<ImportImageCodesCommand, Result<ImportCodesResultDto>>
{
    private static readonly string[] AllowedTypes = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    public async Task<Result<ImportCodesResultDto>> Handle(ImportImageCodesCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.InventoryBatches.FirstOrDefaultAsync(
            b => b.Id == request.BatchId && b.VariantId == request.VariantId, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<ImportCodesResultDto>(CatalogErrors.BatchNotFound);
        }

        var variantStatus = await db.ProductVariants
            .AsNoTracking()
            .Where(v => v.Id == request.VariantId)
            .Select(v => (ProductVariantStatus?)v.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (variantStatus != ProductVariantStatus.Active)
        {
            return Result.Failure<ImportCodesResultDto>(CatalogErrors.VariantNotActive);
        }

        var imported = 0;
        var invalid = 0;

        foreach (var file in request.Files)
        {
            var contentType = file.ContentType.ToLowerInvariant();
            if (file.Length <= 0
                || file.Length > fileStorage.MaxFileSizeBytes
                || !AllowedTypes.Contains(contentType)
                || !fileStorage.IsAllowedContentType(contentType))
            {
                invalid++;
                continue;
            }

            StoredFileResult stored;
            try
            {
                stored = await fileStorage.SaveAsync(file.Content, file.FileName, contentType, ImageCodes.Folder, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                invalid++;
                continue;
            }

            db.DigitalInventoryCodes.Add(DigitalInventoryCode.Create(
                request.VariantId,
                request.BatchId,
                ImageCodes.ToCodeValue(stored.StorageKey),
                batch.SupplierId,
                purchaseCost: batch.PurchaseCost,
                currency: batch.Currency,
                notes: Path.GetFileName(file.FileName)));
            imported++;
        }

        if (imported > 0)
        {
            batch.RecordImport(imported);
            db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
                InventoryAuditAction.CodeImported,
                variantId: request.VariantId,
                batchId: request.BatchId,
                performedByUserId: currentUser.UserId,
                details: $"Imported {imported} of {request.Files.Count} image codes ({invalid} invalid)"));
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportCodesResultDto(request.Files.Count, imported, 0, invalid, []));
    }
}
