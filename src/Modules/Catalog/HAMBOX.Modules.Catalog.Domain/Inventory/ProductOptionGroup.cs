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
    public IReadOnlyCollection<ProductOption> Options => _options.AsReadOnly();
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static ProductOptionGroup Create(Guid productId, string key, string displayName, int sortOrder = 0, bool isRequired = true, Guid? parentOptionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new ProductOptionGroup(Guid.NewGuid(), productId, parentOptionId, key.Trim().ToLowerInvariant(), displayName.Trim(), sortOrder, isRequired);
    }

    public void Update(string displayName, int sortOrder, bool isRequired)
    {
        DisplayName = displayName.Trim();
        SortOrder = sortOrder;
        IsRequired = isRequired;
    }

    public ProductOption AddOption(string value, string label, int sortOrder = 0)
    {
        var option = ProductOption.Create(Id, value, label, sortOrder);
        _options.Add(option);
        return option;
    }
}
