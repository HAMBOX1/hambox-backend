using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Filters;

/// <summary>
/// One applied filter sheet import: who ran it, what it changed and how to take it back. The same file in the same mode
/// cannot be applied twice while its first application stands.
/// </summary>
public sealed class FilterImportLog : Entity
{
    private FilterImportLog()
    {
    }

    private FilterImportLog(Guid id)
        : base(id)
    {
    }

    /// <summary>"Products" (tags on products) or "Definitions" (filters and their values).</summary>
    public string Kind { get; private set; } = string.Empty;

    public string Mode { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the uploaded file, to refuse applying the very same sheet twice.</summary>
    public string FileHash { get; private set; } = string.Empty;

    public int RowCount { get; private set; }

    public int ChangedCount { get; private set; }

    /// <summary>JSON describing exactly what was added / removed, used to undo the import.</summary>
    public string UndoJson { get; private set; } = "[]";

    public string? CreatedBy { get; private set; }

    public DateTimeOffset AppliedOnUtc { get; private set; }

    public DateTimeOffset? UndoneOnUtc { get; private set; }

    public static FilterImportLog Create(
        string kind, string mode, string fileName, string fileHash, int rowCount, int changedCount, string undoJson, string? createdBy) =>
        new(Guid.NewGuid())
        {
            Kind = kind,
            Mode = mode,
            FileName = fileName.Length > 200 ? fileName[..200] : fileName,
            FileHash = fileHash,
            RowCount = rowCount,
            ChangedCount = changedCount,
            UndoJson = undoJson,
            CreatedBy = createdBy,
            AppliedOnUtc = DateTimeOffset.UtcNow,
        };

    public void MarkUndone() => UndoneOnUtc = DateTimeOffset.UtcNow;
}
