using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.KnowledgeBase;

public sealed class KnowledgeCategory : AggregateRoot, IAuditable, ISoftDeletable
{
    private KnowledgeCategory()
    {
    }

    private KnowledgeCategory(Guid id, string name, string slug)
        : base(id)
    {
        Name = name;
        Slug = slug;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static KnowledgeCategory Create(string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new KnowledgeCategory(Guid.NewGuid(), name.Trim(), Slugify(name)) { SortOrder = sortOrder };
    }

    public void Update(string name, int sortOrder, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Slug = Slugify(name);
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }

    private static string Slugify(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');
}
