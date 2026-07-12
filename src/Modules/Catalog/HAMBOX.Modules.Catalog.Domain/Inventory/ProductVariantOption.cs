using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class ProductVariantOption : Entity
{
    private ProductVariantOption()
    {
    }

    private ProductVariantOption(Guid variantId, Guid optionId)
        : base(Guid.NewGuid())
    {
        VariantId = variantId;
        OptionId = optionId;
    }

    public Guid VariantId { get; private set; }
    public Guid OptionId { get; private set; }

    public static ProductVariantOption Create(Guid variantId, Guid optionId) =>
        new(variantId, optionId);
}
