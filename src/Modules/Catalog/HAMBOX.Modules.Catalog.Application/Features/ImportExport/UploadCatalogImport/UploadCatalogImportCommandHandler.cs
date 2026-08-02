using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.UploadCatalogImport;

internal sealed class UploadCatalogImportCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage)
    : IRequestHandler<UploadCatalogImportCommand, Result<CatalogPackageJobDto>>
{
    public async Task<Result<CatalogPackageJobDto>> Handle(
        UploadCatalogImportCommand request, CancellationToken cancellationToken)
    {
        var format = DetectFormat(request.FileName, request.ContentType);
        if (format is null)
        {
            return Result.Failure<CatalogPackageJobDto>(CatalogErrors.UnsupportedPackageFormat);
        }

        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > fileStorage.MaxFileSizeBytes)
        {
            return Result.Failure<CatalogPackageJobDto>(CatalogErrors.UnsupportedPackageFormat);
        }

        // The client-reported content type is unreliable for these formats — .hambox has no
        // registered MIME type anywhere, so every real browser sends "application/octet-stream"
        // for it (and often for .xlsx/.csv too, depending on the OS's MIME database). DetectFormat
        // already trusts the file extension, which is the reliable signal here; save under a
        // canonical content type we control (and have allow-listed) rather than the raw header,
        // so IsAllowedContentType's gate doesn't reject a perfectly valid upload.
        var canonicalContentType = CanonicalContentType(format.Value);

        StoredFileResult stored;
        try
        {
            stored = await fileStorage.SaveAsync(
                request.Content, request.FileName, canonicalContentType, "catalog-imports", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CatalogPackageJobDto>(CatalogErrors.UnsupportedPackageFormat);
        }

        var entityType = format == CatalogPackageFormat.Hambox
            ? CatalogImportEntityType.FullPackage
            : request.EntityType;

        var job = CatalogPackageJob.CreateUpload(format.Value, entityType, stored.StorageKey, stored.FileName, stored.FileSizeBytes);
        dbContext.CatalogPackageJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(CatalogPackageJobMapper.ToDto(job));
    }

    private static CatalogPackageFormat? DetectFormat(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".hambox" => CatalogPackageFormat.Hambox,
            // .xlsm is the macro-enabled OOXML variant of .xlsx — same zip/worksheet structure,
            // so it's parsed by the exact same ClosedXML path. ClosedXML never executes the
            // embedded VBA project, it just doesn't read it, so macros are inert on import.
            ".xlsx" or ".xlsm" => CatalogPackageFormat.Xlsx,
            ".csv" => CatalogPackageFormat.Csv,
            _ => contentType switch
            {
                "application/zip" or "application/x-zip-compressed" => CatalogPackageFormat.Hambox,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => CatalogPackageFormat.Xlsx,
                "application/vnd.ms-excel.sheet.macroEnabled.12" => CatalogPackageFormat.Xlsx,
                "text/csv" => CatalogPackageFormat.Csv,
                _ => null,
            },
        };
    }

    private static string CanonicalContentType(CatalogPackageFormat format) => format switch
    {
        CatalogPackageFormat.Hambox => "application/zip",
        CatalogPackageFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        CatalogPackageFormat.Csv => "text/csv",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
