using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class ProductOption : Entity, IAuditable
{
    private ProductOption()
    {
    }

    private ProductOption(Guid id, Guid optionGroupId, string value, string label, int sortOrder)
        : base(id)
    {
        OptionGroupId = optionGroupId;
        Value = value;
        Label = label;
        SortOrder = sortOrder;
    }

    public Guid OptionGroupId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static ProductOption Create(Guid optionGroupId, string value, string label, int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new ProductOption(Guid.NewGuid(), optionGroupId, value.Trim().ToLowerInvariant(), label.Trim(), sortOrder);
    }

    public void Update(string label, int sortOrder)
    {
        Label = label.Trim();
        SortOrder = sortOrder;
    }
}
