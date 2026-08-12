using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Content.Domain.Faqs;

/// <summary>
/// A lightweight taxonomy label FAQs are grouped under (e.g. "Billing", "Orders"). Deliberately no
/// admin CRUD page of its own — categories are created inline from the FAQ form's "quick add" affordance
/// (see <c>CreateFaqCategoryCommand</c>) and never renamed/deleted through the API today.
/// </summary>
public sealed class FaqCategory : AggregateRoot, IAuditable, ISoftDeletable
{
    private FaqCategory()
    {
    }

    private FaqCategory(Guid id, string nameEn, string? nameAr, string slug, int sortOrder)
        : base(id)
    {
        NameEn = nameEn;
        NameAr = nameAr;
        Slug = slug;
        SortOrder = sortOrder;
    }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static FaqCategory Create(string nameEn, string? nameAr, int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        return new FaqCategory(Guid.NewGuid(), nameEn.Trim(), string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim(), NormalizeSlug(nameEn), sortOrder);
    }

    private static string NormalizeSlug(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '-');
}
