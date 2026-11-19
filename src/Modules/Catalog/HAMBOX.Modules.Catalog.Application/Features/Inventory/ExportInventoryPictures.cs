using System.IO.Compression;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// "Get the pictures I uploaded as stock back": a .zip with every picture code of a variant in the given status (the
/// unsold ones by default), named after the file that was uploaded. The text download cannot carry pictures, so this is
/// its counterpart. Same permission and audit trail as downloading the text codes.
/// </summary>
public sealed record ExportInventoryPicturesQuery(
    Guid VariantId,
    string? Status,
    string? Reason,
    string IpAddress,
    string UserAgent) : IRequest<Result<ExportInventoryPicturesDto>>;

public sealed record ExportInventoryPicturesDto(string FileName, string ContentType, byte[] Content, int PictureCount, int MissingCount);

internal sealed class ExportInventoryPicturesQueryHandler(
    ICatalogDbContext db,
    IFileStorage fileStorage,
    ICurrentUserService currentUser) : IRequestHandler<ExportInventoryPicturesQuery, Result<ExportInventoryPicturesDto>>
{
    private const string DefaultReason = "Stock pictures downloaded as a zip file";

    public async Task<Result<ExportInventoryPicturesDto>> Handle(ExportInventoryPicturesQuery request, CancellationToken cancellationToken)
    {
        var variant = await db.ProductVariants
            .AsNoTracking()
            .Where(v => v.Id == request.VariantId)
            .Select(v => new { v.Id, v.ProductId, v.Sku })
            .FirstOrDefaultAsync(cancellationToken);
        if (variant is null)
        {
            return Result.Failure<ExportInventoryPicturesDto>(CatalogErrors.VariantNotFound);
        }

        var status = InventoryCodeStatus.Available;
        if (!string.IsNullOrWhiteSpace(request.Status)
            && !Enum.TryParse(request.Status, ignoreCase: true, out status))
        {
            return Result.Failure<ExportInventoryPicturesDto>(CatalogErrors.InvalidCodeStatus);
        }

        var codes = await db.DigitalInventoryCodes
            .AsNoTracking()
            .Where(c => c.VariantId == request.VariantId && c.Status == status)
            .OrderBy(c => c.CreatedOnUtc).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var missing = 0;
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var code in codes)
            {
                var key = ImageCodes.ToStorageKey(code.DigitalCode);
                if (key is null || !ImageCodes.IsPrivateCodeImageKey(key))
                {
                    continue;
                }

                try
                {
                    await using var source = await fileStorage.OpenReadAsync(key, cancellationToken);
                    var entry = zip.CreateEntry(UniqueName(code, key, used), CompressionLevel.NoCompression);
                    await using var target = entry.Open();
                    await source.CopyToAsync(target, cancellationToken);
                    added++;
                }
                catch (FileNotFoundException)
                {
                    // The file is gone from disk: keep going and report how many could not be included.
                    missing++;
                }
            }
        }

        if (added == 0 && missing == 0)
        {
            return Result.Failure<ExportInventoryPicturesDto>(CatalogErrors.NoPictureStock);
        }

        db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.CodeExported,
            productId: variant.ProductId,
            variantId: variant.Id,
            performedByUserId: currentUser.UserId,
            details: $"Downloaded {added} {status} stock pictures as a zip ({missing} missing from storage)",
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            reason: string.IsNullOrWhiteSpace(request.Reason) ? DefaultReason : request.Reason.Trim()));
        await db.SaveChangesAsync(cancellationToken);

        var safeSku = new string(variant.Sku.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        return Result.Success(new ExportInventoryPicturesDto(
            $"stock-pictures-{safeSku}-{status.ToString().ToLowerInvariant()}.zip",
            "application/zip",
            buffer.ToArray(),
            added,
            missing));
    }

    /// <summary>The uploaded file name (kept in the code's note), made safe and unique inside the zip.</summary>
    private static string UniqueName(DigitalInventoryCode code, string storageKey, HashSet<string> used)
    {
        var original = Path.GetFileName(code.Notes ?? string.Empty);
        var extension = Path.GetExtension(storageKey);
        var baseName = string.IsNullOrWhiteSpace(original) ? "picture" : Path.GetFileNameWithoutExtension(original);
        baseName = new string(baseName.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ' ? ch : '-').ToArray()).Trim();
        if (baseName.Length == 0)
        {
            baseName = "picture";
        }

        var name = baseName + extension;
        var counter = 2;
        while (!used.Add(name))
        {
            name = $"{baseName}-{counter++}{extension}";
        }

        return name;
    }
}
