using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class ProductOptionGroup : Entity, IAuditable
{
    private readonly List<ProductOption> _options = [];

    private ProductOptionGroup()
    {
    }

    private ProductOptionGroup(Guid id, Guid productId, Guid? parentOptionId, string key, string displayName, int sortOrder, bool isRequired)
        : base(id)
    {
        ProductId = productId;
        ParentOptionId = parentOptionId;
        Key = key;
        DisplayName = displayName;
        SortOrder = sortOrder;
        IsRequired = isRequired;
    }

    public Guid ProductId { get; private set; }
    public Guid? ParentOptionId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsRequired { get; private set; }

    /// <summary>Optional, already-sanitized rich-text instructions for this whole option group (e.g.
    /// "pick the region matching your account's country"), shown to the customer regardless of which
    /// value within the group they pick. Distinct from each <see cref="ProductOption.DescriptionHtml"/>,
    /// which is specific to one value. Sanitization happens in the Application layer before this is set.</summary>
    public string? DescriptionHtml { get; private set; }
    public IReadOnlyCollection<ProductOption> Options => _options.AsReadOnly();
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static ProductOptionGroup Create(Guid productId, string key, string displayName, int sortOrder = 0, bool isRequired = true, Guid? parentOptionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new ProductOptionGroup(Guid.NewGuid(), productId, parentOptionId, key.Trim().ToLowerInvariant(), displayName.Trim(), sortOrder, isRequired);
    }

    public void Update(string displayName, int sortOrder, bool isRequired, string? descriptionHtml = null)
    {
        DisplayName = displayName.Trim();
        SortOrder = sortOrder;
        IsRequired = isRequired;
        DescriptionHtml = descriptionHtml;
    }

    public ProductOption AddOption(string value, string label, int sortOrder = 0)
    {
        var option = ProductOption.Create(Id, value, label, sortOrder);
        _options.Add(option);
        return option;
    }
}
