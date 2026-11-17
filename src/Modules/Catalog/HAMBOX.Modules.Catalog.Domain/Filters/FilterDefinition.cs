using System.Text.Json;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Filters;

/// <summary>
/// A storefront filter the admin created by hand (Game Mode, Offers, Region...), independent of any product's option
/// groups. Products are tagged with its <see cref="FilterValue"/>s; the storefront then offers it exactly like the
/// option-group filters, so it can be ordered, renamed and made conditional from the same filter lists.
/// </summary>
public sealed class FilterDefinition : Entity
{
    private readonly List<FilterValue> _values = [];

    private FilterDefinition()
    {
    }

    private FilterDefinition(Guid id)
        : base(id)
    {
    }

    /// <summary>The lowercase slug used in URLs and filter lists (never shared with an option group key).</summary>
    public string Key { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public string? NameAr { get; private set; }

    /// <summary>True lets a customer tick several values (OR); false allows one value at a time.</summary>
    public bool IsMultiSelect { get; private set; } = true;

    /// <summary>An inactive filter is never shown on the storefront (its data is kept).</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Show the filter (with its values) even when no product in view carries one of its values.</summary>
    public bool ShowWhenEmpty { get; private set; }

    /// <summary>Whether the filter opens expanded on the storefront.</summary>
    public bool DefaultExpanded { get; private set; } = true;

    /// <summary>Order in the library list.</summary>
    public int SortOrder { get; private set; }

    /// <summary>JSON array of category ids (platforms / categories) the filter belongs to; empty = everywhere.</summary>
    public string? ScopeCategoryIds { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? ModifiedOnUtc { get; private set; }

    public IReadOnlyCollection<FilterValue> Values => _values.AsReadOnly();

    public IReadOnlyList<Guid> GetScopeCategoryIds() =>
        string.IsNullOrWhiteSpace(ScopeCategoryIds)
            ? []
            : JsonSerializer.Deserialize<List<Guid>>(ScopeCategoryIds) ?? [];

    public static string NormalizeKey(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
    }

    public static FilterDefinition Create(string key, string nameEn, string? nameAr)
    {
        var normalized = NormalizeKey(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalized);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        var definition = new FilterDefinition(Guid.NewGuid()) { Key = normalized };
        definition.Update(nameEn, nameAr, true, true, false, true, []);
        return definition;
    }

    public void Update(
        string nameEn,
        string? nameAr,
        bool isMultiSelect,
        bool isActive,
        bool showWhenEmpty,
        bool defaultExpanded,
        IReadOnlyCollection<Guid> scopeCategoryIds,
        string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        IsMultiSelect = isMultiSelect;
        IsActive = isActive;
        ShowWhenEmpty = showWhenEmpty;
        DefaultExpanded = defaultExpanded;
        ScopeCategoryIds = scopeCategoryIds.Count == 0 ? null : JsonSerializer.Serialize(scopeCategoryIds.Distinct().ToList());
        ModifiedBy = modifiedBy;
        ModifiedOnUtc = DateTimeOffset.UtcNow;
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public FilterValue AddValue(string value, string labelEn, string? labelAr, int sortOrder)
    {
        var added = FilterValue.Create(Id, value, labelEn, labelAr, sortOrder);
        _values.Add(added);
        return added;
    }
}

/// <summary>One option of a <see cref="FilterDefinition"/> (Single Player, Flash Sale...).</summary>
public sealed class FilterValue : Entity
{
    private FilterValue()
    {
    }

    private FilterValue(Guid id)
        : base(id)
    {
    }

    public Guid FilterId { get; private set; }

    /// <summary>The lowercase slug used in URLs and sheets.</summary>
    public string Value { get; private set; } = string.Empty;

    public string LabelEn { get; private set; } = string.Empty;

    public string? LabelAr { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>An inactive value is hidden from customers; products keep it so it can come back (e.g. a past occasion).</summary>
    public bool IsActive { get; private set; } = true;

    public static FilterValue Create(Guid filterId, string value, string labelEn, string? labelAr, int sortOrder)
    {
        var slug = FilterDefinition.NormalizeKey(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelEn);

        var created = new FilterValue(Guid.NewGuid()) { FilterId = filterId, Value = slug };
        created.Update(labelEn, labelAr, sortOrder, true);
        return created;
    }

    public void Update(string labelEn, string? labelAr, int sortOrder, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelEn);

        LabelEn = labelEn.Trim();
        LabelAr = string.IsNullOrWhiteSpace(labelAr) ? null : labelAr.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}

/// <summary>Tags a product with one value of a custom filter.</summary>
public sealed class ProductFilterValue : Entity
{
    private ProductFilterValue()
    {
    }

    private ProductFilterValue(Guid id)
        : base(id)
    {
    }

    public Guid ProductId { get; private set; }

    public Guid FilterId { get; private set; }

    public Guid FilterValueId { get; private set; }

    public static ProductFilterValue Create(Guid productId, Guid filterId, Guid filterValueId) =>
        new(Guid.NewGuid()) { ProductId = productId, FilterId = filterId, FilterValueId = filterValueId };
}
