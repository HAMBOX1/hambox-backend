using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class ProductOption : Entity, IAuditable
{
    private ProductOption()
    {
    }

    private ProductOption(Guid id, Guid optionGroupId, string value, string label, int sortOrder, string? descriptionHtml)
        : base(id)
    {
        OptionGroupId = optionGroupId;
        Value = value;
        Label = label;
        SortOrder = sortOrder;
        DescriptionHtml = descriptionHtml;
    }

    public Guid OptionGroupId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    /// <summary>Optional, already-sanitized rich-text instructions for this value (e.g. regional
    /// activation requirements). Null means no description — callers must never pass raw
    /// unsanitized HTML here; sanitization happens in the Application layer before this is set.</summary>
    public string? DescriptionHtml { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static ProductOption Create(Guid optionGroupId, string value, string label, int sortOrder = 0, string? descriptionHtml = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new ProductOption(Guid.NewGuid(), optionGroupId, value.Trim().ToLowerInvariant(), label.Trim(), sortOrder, descriptionHtml);
    }

    public void Update(string label, int sortOrder, string? descriptionHtml)
    {
        Label = label.Trim();
        SortOrder = sortOrder;
        DescriptionHtml = descriptionHtml;
    }
}
