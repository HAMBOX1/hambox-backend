using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

/// <summary>
/// A reusable, product-independent snapshot of an option group (name + values) an admin can
/// import into any product's option groups. Importing creates ordinary, independent
/// <see cref="ProductOptionGroup"/>/<see cref="ProductOption"/> rows — this template is never
/// referenced by a product after import, so editing or deleting it can never affect a product
/// that already imported it.
/// </summary>
public sealed class OptionGroupTemplate : Entity, IAuditable
{
    private readonly List<OptionGroupTemplateOption> _options = [];

    private OptionGroupTemplate()
    {
    }

    private OptionGroupTemplate(Guid id, string name, bool isRequiredDefault)
        : base(id)
    {
        Name = name;
        IsRequiredDefault = isRequiredDefault;
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsRequiredDefault { get; private set; }
    public IReadOnlyCollection<OptionGroupTemplateOption> Options => _options.AsReadOnly();
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static OptionGroupTemplate Create(string name, bool isRequiredDefault = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new OptionGroupTemplate(Guid.NewGuid(), name.Trim(), isRequiredDefault);
    }

    public void Update(string name, bool isRequiredDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        IsRequiredDefault = isRequiredDefault;
    }

    /// <summary>Replaces the entire option set — templates are edited as one unit, not via
    /// granular per-option CRUD like live product options. Returns the newly created options so
    /// callers updating an already-tracked template can explicitly <c>Add</c> them: EF Core's
    /// automatic graph fixup misjudges client-generated Guid-keyed entities reached only through a
    /// tracked parent's navigation as "Modified" rather than "Added".</summary>
    public IReadOnlyList<OptionGroupTemplateOption> ReplaceOptions(IEnumerable<(string Value, string Label, int SortOrder, string? DescriptionHtml)> options)
    {
        _options.Clear();
        var created = new List<OptionGroupTemplateOption>();
        foreach (var (value, label, sortOrder, descriptionHtml) in options)
        {
            var option = OptionGroupTemplateOption.Create(Id, value, label, sortOrder, descriptionHtml);
            _options.Add(option);
            created.Add(option);
        }

        return created;
    }
}
