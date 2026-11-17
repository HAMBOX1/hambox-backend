using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Filters;

/// <summary>
/// A snapshot of a category's filter list taken just before it was changed (saved, reset or restored): who changed it,
/// when, and what it looked like before, so any earlier version can be brought back.
/// </summary>
public sealed class FilterListHistory : Entity
{
    private FilterListHistory()
    {
    }

    private FilterListHistory(Guid id)
        : base(id)
    {
    }

    public Guid CategoryId { get; private set; }

    /// <summary>"Saved", "Reset" or "Restored": what was done to the list.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>JSON of the list as it was before the change.</summary>
    public string SnapshotJson { get; private set; } = "[]";

    public int ItemCount { get; private set; }

    public string? ChangedBy { get; private set; }

    public DateTimeOffset ChangedOnUtc { get; private set; }

    public static FilterListHistory Create(Guid categoryId, string action, string snapshotJson, int itemCount, string? changedBy) =>
        new(Guid.NewGuid())
        {
            CategoryId = categoryId,
            Action = action,
            SnapshotJson = snapshotJson,
            ItemCount = itemCount,
            ChangedBy = changedBy,
            ChangedOnUtc = DateTimeOffset.UtcNow,
        };
}
