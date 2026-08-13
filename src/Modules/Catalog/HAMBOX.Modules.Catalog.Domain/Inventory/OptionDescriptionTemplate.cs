using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

/// <summary>
/// A reusable, product-independent snapshot of option-value instructions (e.g. "Global Activation
/// Instructions") an admin can copy into any <see cref="ProductOption"/>'s DescriptionHtml.
/// Selecting one copies its sanitized DescriptionHtml at that moment — this template is never
/// referenced by a product option afterward, so editing or deleting it can never affect a product
/// option that already copied from it.
/// </summary>
public sealed class OptionDescriptionTemplate : Entity, IAuditable
{
    private OptionDescriptionTemplate()
    {
    }

    private OptionDescriptionTemplate(Guid id, string name, string descriptionHtml)
        : base(id)
    {
        Name = name;
        DescriptionHtml = descriptionHtml;
    }

    public string Name { get; private set; } = string.Empty;
    public string DescriptionHtml { get; private set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static OptionDescriptionTemplate Create(string name, string descriptionHtml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionHtml);
        return new OptionDescriptionTemplate(Guid.NewGuid(), name.Trim(), descriptionHtml);
    }

    public void Update(string name, string descriptionHtml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionHtml);
        Name = name.Trim();
        DescriptionHtml = descriptionHtml;
    }
}
