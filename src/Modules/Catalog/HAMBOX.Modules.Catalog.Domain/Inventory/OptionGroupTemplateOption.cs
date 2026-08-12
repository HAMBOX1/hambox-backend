using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

/// <summary>One saved value within an <see cref="OptionGroupTemplate"/> — mirrors the fields of
/// <see cref="ProductOption"/> minus the product-scoped OptionGroupId.</summary>
public sealed class OptionGroupTemplateOption : Entity, IAuditable
{
    private OptionGroupTemplateOption()
    {
    }

    private OptionGroupTemplateOption(Guid id, Guid templateId, string value, string label, int sortOrder, string? descriptionHtml)
        : base(id)
    {
        TemplateId = templateId;
        Value = value;
        Label = label;
        SortOrder = sortOrder;
        DescriptionHtml = descriptionHtml;
    }

    public Guid TemplateId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public string? DescriptionHtml { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static OptionGroupTemplateOption Create(Guid templateId, string value, string label, int sortOrder = 0, string? descriptionHtml = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new OptionGroupTemplateOption(Guid.NewGuid(), templateId, value.Trim().ToLowerInvariant(), label.Trim(), sortOrder, descriptionHtml);
    }
}
