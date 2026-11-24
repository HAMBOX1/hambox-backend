using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Business;

/// <summary>
/// A wholesale price for a product (optionally one variant) from a minimum quantity, either for one business account or —
/// with a null <see cref="BusinessAccountId"/> — for every approved business. Prices are USD, like every stored price.
/// Two accounts can hold different prices for the same SKU; the most specific, highest-quantity-tier price wins.
/// </summary>
public sealed class BusinessPrice : Entity
{
    private BusinessPrice()
    {
    }

    private BusinessPrice(Guid id)
        : base(id)
    {
    }

    public Guid? BusinessAccountId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? ProductVariantId { get; private set; }

    public int MinQuantity { get; private set; } = 1;

    public decimal UnitPriceUsd { get; private set; }

    public DateTimeOffset? ValidFromUtc { get; private set; }

    public DateTimeOffset? ValidToUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static BusinessPrice Create(
        Guid? accountId, Guid productId, Guid? variantId, int minQuantity, decimal unitPriceUsd, DateTimeOffset? from, DateTimeOffset? to)
    {
        var price = new BusinessPrice(Guid.NewGuid()) { BusinessAccountId = accountId, ProductId = productId };
        price.Update(variantId, minQuantity, unitPriceUsd, from, to, true);
        return price;
    }

    public void Update(Guid? variantId, int minQuantity, decimal unitPriceUsd, DateTimeOffset? from, DateTimeOffset? to, bool isActive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minQuantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(unitPriceUsd, 0m);
        if (from is { } f && to is { } t && t <= f)
        {
            throw new ArgumentException("The end must be after the start.", nameof(to));
        }

        ProductVariantId = variantId;
        MinQuantity = minQuantity;
        UnitPriceUsd = decimal.Round(unitPriceUsd, 2);
        ValidFromUtc = from;
        ValidToUtc = to;
        IsActive = isActive;
    }

    public bool AppliesAt(DateTimeOffset now) =>
        IsActive && (ValidFromUtc is null || ValidFromUtc <= now) && (ValidToUtc is null || ValidToUtc > now);
}
