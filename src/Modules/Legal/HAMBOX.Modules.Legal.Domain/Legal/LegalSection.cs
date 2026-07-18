using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Legal.Domain.Legal;

/// <summary>
/// Aggregate root for a single admin-managed legal section (Terms, Privacy, or any section an
/// admin creates). Sections are identified by a unique <see cref="Slug"/> rather than a fixed type.
/// </summary>
public sealed class LegalSection : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<LegalSectionVersion> _versions = [];

    private LegalSection()
    {
    }

    private LegalSection(Guid id, string slug)
        : base(id)
    {
        Slug = slug;
    }

    public string Slug { get; private set; } = string.Empty;
    public string? Category { get; private set; }
    public string? Icon { get; private set; }
    public int SortOrder { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public string? SeoTitle { get; private set; }
    public string? SeoDescription { get; private set; }
    public string? SeoKeywords { get; private set; }
    public bool ShowInFooter { get; private set; } = true;
    public bool ShowInNavigation { get; private set; } = true;
    public bool RequireAcceptance { get; private set; }
    public bool IsArchived { get; private set; }
    public Guid? PublishedVersionId { get; private set; }
    public int VersionCounter { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    public IReadOnlyCollection<LegalSectionVersion> Versions => _versions.AsReadOnly();

    public static LegalSection Create(string slug) => new(Guid.NewGuid(), slug);

    public LegalSectionVersion CreateDraftVersion(string titleEn, string? titleAr, string contentEn, string? contentAr, string? versionNotes)
    {
        VersionCounter++;
        var version = LegalSectionVersion.CreateDraft(Id, VersionCounter, titleEn, titleAr, contentEn, contentAr, versionNotes);
        _versions.Add(version);
        return version;
    }

    /// <summary>
    /// The version awaiting publish, if any. Must be the single most-recent version overall —
    /// not merely "any unpublished version" — because publishing an edit unpublishes the
    /// previously-live version too, and that older version must never be mistaken for the draft.
    /// </summary>
    public LegalSectionVersion? GetCurrentDraft()
    {
        var latest = _versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        return latest is not null && !latest.IsPublished ? latest : null;
    }

    public LegalSectionVersion PublishVersion(Guid versionId, string? publishedBy)
    {
        var version = _versions.SingleOrDefault(v => v.Id == versionId)
            ?? throw new InvalidOperationException("Legal section version not found.");

        foreach (var existing in _versions.Where(v => v.IsPublished))
        {
            existing.Unpublish();
        }

        version.Publish(publishedBy);
        PublishedVersionId = version.Id;
        return version;
    }

    public void UpdateMetadata(
        string slug,
        string? category,
        string? icon,
        int sortOrder,
        string? descriptionEn,
        string? descriptionAr,
        string? seoTitle,
        string? seoDescription,
        string? seoKeywords,
        bool showInFooter,
        bool showInNavigation,
        bool requireAcceptance)
    {
        Slug = slug;
        Category = category;
        Icon = icon;
        SortOrder = sortOrder;
        DescriptionEn = descriptionEn;
        DescriptionAr = descriptionAr;
        SeoTitle = seoTitle;
        SeoDescription = seoDescription;
        SeoKeywords = seoKeywords;
        ShowInFooter = showInFooter;
        ShowInNavigation = showInNavigation;
        RequireAcceptance = requireAcceptance;
    }

    public void SetArchived(bool archived) => IsArchived = archived;
}
