using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class ProductPlan : Entity, IAuditable
{
    private ProductPlan()
    {
    }

    private ProductPlan(Guid id, Guid productId, string name, string slug, int sortOrder)
        : base(id)
    {
        ProductId = productId;
        Name = name;
        Slug = slug;
        SortOrder = sortOrder;
        Status = ProductVariantStatus.Active;
    }

    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public ProductVariantStatus Status { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static ProductPlan Create(Guid productId, string name, string slug, int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return new ProductPlan(Guid.NewGuid(), productId, name.Trim(), slug.Trim().ToLowerInvariant(), sortOrder);
    }

    public void Update(string name, string slug, int sortOrder, ProductVariantStatus status)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        SortOrder = sortOrder;
        Status = status;
    }
}
