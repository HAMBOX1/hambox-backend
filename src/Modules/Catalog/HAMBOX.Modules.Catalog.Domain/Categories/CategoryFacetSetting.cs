using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Categories;

/// <summary>
/// One entry of a category's storefront filter list: which option group (Platform, Region, ...) is offered as
/// a filter when customers browse that category, in what order, and under what name. A category with no rows
/// inherits its nearest ancestor's list, then the store-wide default (<see cref="DefaultCategoryId"/>); only if
/// none of those exist does the storefront fall back to offering every option group.
/// </summary>
public sealed class CategoryFacetSetting : AggregateRoot
{
    /// <summary>The pseudo category id that holds the store-wide default filter list.</summary>
    public static readonly Guid DefaultCategoryId = Guid.Empty;

    private CategoryFacetSetting()
    {
    }

    private CategoryFacetSetting(
        Guid id,
        Guid categoryId,
        string groupKey,
        string? displayNameEn,
        string? displayNameAr,
        int sortOrder,
        bool isVisible)
        : base(id)
    {
        CategoryId = categoryId;
        GroupKey = groupKey;
        DisplayNameEn = displayNameEn;
        DisplayNameAr = displayNameAr;
        SortOrder = sortOrder;
        IsVisible = isVisible;
    }

    public Guid CategoryId { get; private set; }
    public string GroupKey { get; private set; } = string.Empty;
    public string? DisplayNameEn { get; private set; }
    public string? DisplayNameAr { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsVisible { get; private set; }

    public static CategoryFacetSetting Create(
        Guid categoryId,
        string groupKey,
        string? displayNameEn,
        string? displayNameAr,
        int sortOrder,
        bool isVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);

        return new CategoryFacetSetting(
            Guid.NewGuid(),
            categoryId,
            groupKey.Trim(),
            string.IsNullOrWhiteSpace(displayNameEn) ? null : displayNameEn.Trim(),
            string.IsNullOrWhiteSpace(displayNameAr) ? null : displayNameAr.Trim(),
            sortOrder,
            isVisible);
    }
}
