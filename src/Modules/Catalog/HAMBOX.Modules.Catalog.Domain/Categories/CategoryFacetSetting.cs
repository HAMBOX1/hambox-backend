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
        bool isVisible,
        string? dependsOnGroupKey,
        IReadOnlyList<string>? dependsOnValues)
        : base(id)
    {
        CategoryId = categoryId;
        GroupKey = groupKey;
        DisplayNameEn = displayNameEn;
        DisplayNameAr = displayNameAr;
        SortOrder = sortOrder;
        IsVisible = isVisible;
        DependsOnGroupKey = dependsOnGroupKey;
        DependsOnValues = dependsOnValues is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(dependsOnValues) : null;
    }

    public Guid CategoryId { get; private set; }
    public string GroupKey { get; private set; } = string.Empty;
    public string? DisplayNameEn { get; private set; }
    public string? DisplayNameAr { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsVisible { get; private set; }

    /// <summary>
    /// When set, the filter is only offered once the customer has picked one of <see cref="DependsOnValues"/> in the
    /// filter group <see cref="DependsOnGroupKey"/> (e.g. "Duration" only after "Product type = Subscription").
    /// </summary>
    public string? DependsOnGroupKey { get; private set; }

    /// <summary>JSON array of the option values of <see cref="DependsOnGroupKey"/> that switch this filter on.</summary>
    public string? DependsOnValues { get; private set; }

    /// <summary>The parent values as a list (empty when the filter has no condition).</summary>
    public IReadOnlyList<string> GetDependsOnValues() =>
        string.IsNullOrWhiteSpace(DependsOnValues)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(DependsOnValues) ?? [];

    public static CategoryFacetSetting Create(
        Guid categoryId,
        string groupKey,
        string? displayNameEn,
        string? displayNameAr,
        int sortOrder,
        bool isVisible,
        string? dependsOnGroupKey = null,
        IReadOnlyList<string>? dependsOnValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);

        return new CategoryFacetSetting(
            Guid.NewGuid(),
            categoryId,
            groupKey.Trim(),
            string.IsNullOrWhiteSpace(displayNameEn) ? null : displayNameEn.Trim(),
            string.IsNullOrWhiteSpace(displayNameAr) ? null : displayNameAr.Trim(),
            sortOrder,
            isVisible,
            string.IsNullOrWhiteSpace(dependsOnGroupKey) || dependsOnValues is not { Count: > 0 } ? null : dependsOnGroupKey.Trim(),
            dependsOnValues is { Count: > 0 } && !string.IsNullOrWhiteSpace(dependsOnGroupKey)
                ? dependsOnValues.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : null);
    }
}
