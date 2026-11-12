using System.Text;
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
/// "Get the stock back as a text file": every text code of a variant in the given status (the unsold ones by default),
/// in plain text, one entry per line. The normal CSV export masks codes on purpose, so this is the explicit,
/// permission-gated, audited way to take the real codes out — the same rules as revealing a single code.
/// Pictures cannot be written into a text file; they are left out and counted so the admin knows.
/// </summary>
public sealed record ExportInventoryCodesTextQuery(
    Guid VariantId,
    string? Status,
    string? Reason,
    string IpAddress,
    string UserAgent) : IRequest<Result<ExportInventoryCodesTextDto>>;

public sealed record ExportInventoryCodesTextDto(string FileName, string ContentType, byte[] Content, int CodeCount, int PictureCount);

internal sealed class ExportInventoryCodesTextQueryHandler(
    ICatalogDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<ExportInventoryCodesTextQuery, Result<ExportInventoryCodesTextDto>>
{
    private const string DefaultReason = "Stock downloaded as a text file";

    public async Task<Result<ExportInventoryCodesTextDto>> Handle(
        ExportInventoryCodesTextQuery request, CancellationToken cancellationToken)
    {
        var variant = await db.ProductVariants
            .AsNoTracking()
            .Where(v => v.Id == request.VariantId)
            .Select(v => new { v.Id, v.ProductId, v.Sku })
            .FirstOrDefaultAsync(cancellationToken);
        if (variant is null)
        {
            return Result.Failure<ExportInventoryCodesTextDto>(CatalogErrors.VariantNotFound);
        }

        var status = InventoryCodeStatus.Available;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse(request.Status, ignoreCase: true, out status))
            {
                return Result.Failure<ExportInventoryCodesTextDto>(CatalogErrors.InvalidCodeStatus);
            }
        }

        // Codes are encrypted at rest, so ordering by the raw column would sort by ciphertext: order by creation time
        // (codes saved together share a timestamp, so the id keeps the order stable between downloads).
        var codes = await db.DigitalInventoryCodes
            .AsNoTracking()
            .Where(c => c.VariantId == request.VariantId && c.Status == status)
            .OrderBy(c => c.CreatedOnUtc).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var entries = new List<string>(codes.Count);
        var pictures = 0;
        foreach (var code in codes)
        {
            if (ImageCodes.IsImageCode(code.DigitalCode))
            {
                pictures++;
                continue;
            }

            entries.Add(code.DigitalCode);
        }

        // A multi-line entry (e.g. an account with e-mail + password lines) is kept together and separated by a
        // blank line, so it can be pasted straight back with "the whole text is ONE entry".
        var separator = entries.Any(e => e.Contains('\n')) ? "\n\n" : "\n";
        var text = string.Join(separator, entries.Select(e => e.Replace("\r\n", "\n").TrimEnd()));
        if (entries.Count > 0)
        {
            text += "\n";
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? DefaultReason : request.Reason.Trim();
        db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.CodeExported,
            productId: variant.ProductId,
            variantId: variant.Id,
            performedByUserId: currentUser.UserId,
            details: $"Downloaded {entries.Count} {status} codes as plain text ({pictures} pictures left out)",
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            reason: reason));
        await db.SaveChangesAsync(cancellationToken);

        var safeSku = new string(variant.Sku.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        return Result.Success(new ExportInventoryCodesTextDto(
            $"stock-{safeSku}-{status.ToString().ToLowerInvariant()}.txt",
            "text/plain; charset=utf-8",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text),
            entries.Count,
            pictures));
    }
}
