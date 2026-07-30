using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.SavedReplies;

public sealed class SavedReplyFolder : AggregateRoot, IAuditable, ISoftDeletable
{
    private SavedReplyFolder()
    {
    }

    private SavedReplyFolder(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static SavedReplyFolder Create(string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SavedReplyFolder(Guid.NewGuid(), name.Trim()) { SortOrder = sortOrder };
    }

    public void Update(string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        SortOrder = sortOrder;
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}
