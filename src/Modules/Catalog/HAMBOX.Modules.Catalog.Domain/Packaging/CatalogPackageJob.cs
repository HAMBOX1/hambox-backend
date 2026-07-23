using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Packaging;

/// <summary>
/// Tracks one catalog import or export package end-to-end: the uploaded/generated file's storage
/// location, progress, and final summary. Deliberately self-contained rather than reading
/// Commerce's <c>OperationalJob</c> table — Catalog has no reference to Commerce, so this is the
/// row the wizard's Validate/Execute/Summary steps read directly. The background job handler also
/// reports progress through the generic <c>IBackgroundJobContext</c> so the job shows up on the
/// cross-cutting Operations dashboard, but this row carries the catalog-specific payload
/// (row counts, storage keys) that the generic job table has no place for.
/// </summary>
public sealed class CatalogPackageJob : AggregateRoot, IAuditable
{
    private CatalogPackageJob()
    {
    }

    private CatalogPackageJob(
        Guid id,
        CatalogPackageDirection direction,
        CatalogPackageFormat format,
        CatalogImportEntityType entityType,
        string storageKey,
        string fileName,
        long fileSizeBytes)
        : base(id)
    {
        Direction = direction;
        Format = format;
        EntityType = entityType;
        StorageKey = storageKey;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        Status = CatalogPackageJobStatus.Uploaded;
    }

    public CatalogPackageDirection Direction { get; private set; }
    public CatalogPackageFormat Format { get; private set; }
    public CatalogImportEntityType EntityType { get; private set; }

    /// <summary>Uploaded source file (Import) — empty until the export job finishes writing one (Export).</summary>
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }

    public CatalogPackageJobStatus Status { get; private set; }
    public int ProgressPercent { get; private set; }
    public Guid? BackgroundJobId { get; private set; }

    /// <summary>The generated export artifact, or an import error report — set on completion.</summary>
    public string? ResultStorageKey { get; private set; }
    public string? ResultFileName { get; private set; }

    /// <summary>JSON-serialized <c>CatalogPackageSummary</c> (counts created/updated/skipped/failed).</summary>
    public string? SummaryJson { get; private set; }
    public string? ErrorMessage { get; private set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static CatalogPackageJob CreateUpload(
        CatalogPackageFormat format,
        CatalogImportEntityType entityType,
        string storageKey,
        string fileName,
        long fileSizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return new CatalogPackageJob(
            Guid.NewGuid(), CatalogPackageDirection.Import, format, entityType, storageKey, fileName, fileSizeBytes);
    }

    public static CatalogPackageJob CreateExportRequest(CatalogPackageFormat format)
    {
        return new CatalogPackageJob(
            Guid.NewGuid(), CatalogPackageDirection.Export, format, CatalogImportEntityType.FullPackage, string.Empty, string.Empty, 0);
    }

    public void AttachBackgroundJob(Guid backgroundJobId)
    {
        BackgroundJobId = backgroundJobId;
        Status = CatalogPackageJobStatus.Queued;
    }

    public void ReportProgress(int percent)
    {
        ProgressPercent = Math.Clamp(percent, 0, 100);
        if (Status == CatalogPackageJobStatus.Queued)
        {
            Status = CatalogPackageJobStatus.Processing;
        }
    }

    public void MarkCompleted(string? resultStorageKey, string? resultFileName, string summaryJson)
    {
        Status = CatalogPackageJobStatus.Completed;
        ResultStorageKey = resultStorageKey;
        ResultFileName = resultFileName;
        SummaryJson = summaryJson;
        ProgressPercent = 100;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = CatalogPackageJobStatus.Failed;
        ErrorMessage = errorMessage;
    }
}
