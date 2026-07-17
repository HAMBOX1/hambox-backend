using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Legal.Domain.Legal;

public sealed class LegalSectionVersion : BaseEntity
{
    private LegalSectionVersion()
    {
    }

    private LegalSectionVersion(
        Guid id,
        Guid legalSectionId,
        int versionNumber,
        string titleEn,
        string? titleAr,
        string contentEn,
        string? contentAr,
        string? versionNotes)
        : base(id)
    {
        LegalSectionId = legalSectionId;
        VersionNumber = versionNumber;
        TitleEn = titleEn;
        TitleAr = titleAr;
        ContentEn = contentEn;
        ContentAr = contentAr;
        VersionNotes = versionNotes;
        IsPublished = false;
    }

    public Guid LegalSectionId { get; private set; }
    public int VersionNumber { get; private set; }
    public string TitleEn { get; private set; } = string.Empty;
    public string? TitleAr { get; private set; }
    public string ContentEn { get; private set; } = string.Empty;
    public string? ContentAr { get; private set; }
    public string? VersionNotes { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? PublishedOnUtc { get; private set; }
    public string? PublishedBy { get; private set; }

    public static LegalSectionVersion CreateDraft(
        Guid legalSectionId, int versionNumber, string titleEn, string? titleAr, string contentEn, string? contentAr, string? versionNotes) =>
        new(Guid.NewGuid(), legalSectionId, versionNumber, titleEn, titleAr, contentEn, contentAr, versionNotes);

    public void UpdateContent(string titleEn, string? titleAr, string contentEn, string? contentAr, string? versionNotes)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("Published versions are immutable.");
        }

        TitleEn = titleEn;
        TitleAr = titleAr;
        ContentEn = contentEn;
        ContentAr = contentAr;
        VersionNotes = versionNotes;
    }

    public void Publish(string? publishedBy = null)
    {
        IsPublished = true;
        PublishedOnUtc = DateTime.UtcNow;
        PublishedBy = publishedBy;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }
}
