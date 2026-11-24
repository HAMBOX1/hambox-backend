using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class ProductVariant : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<ProductVariantOption> _selectedOptions = [];

    private ProductVariant()
    {
    }

    private ProductVariant(
        Guid id,
        Guid productId,
        Guid? planId,
        string sku,
        decimal? priceOverride,
        decimal? comparePrice,
        int sortOrder,
        int lowStockThreshold)
        : base(id)
    {
        ProductId = productId;
        PlanId = planId;
        Sku = sku;
        PriceOverride = priceOverride;
        ComparePrice = comparePrice;
        SortOrder = sortOrder;
        LowStockThreshold = lowStockThreshold;
        Status = ProductVariantStatus.Draft;
        IsVisible = false;
        FulfillmentMode = FulfillmentMode.ManualOnly;
    }

    public Guid ProductId { get; private set; }
    public Guid? PlanId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public decimal? PriceOverride { get; private set; }
    public decimal? ComparePrice { get; private set; }
    /// <summary>What this variant costs to acquire — the basis for computing <see cref="PriceOverride"/>
    /// or <see cref="MemberPrice"/> as a percentage markup in the admin pricing UI. Purely informational
    /// at checkout time; never itself charged to a customer.</summary>
    public decimal? CostPrice { get; private set; }
    /// <summary>The ELITE member price: charged at checkout (by Commerce's cart line validation, not by
    /// <see cref="Services.EffectivePriceResolver"/>) to customers with an active ELITE subscription when it is lower
    /// than the price they would otherwise pay.</summary>
    public decimal? MemberPrice { get; private set; }

    /// <summary>A temporary price below the normal one. It applies only between <see cref="SaleStartsOn"/> and <see cref="SaleEndsOn"/> (either may be open-ended).</summary>
    public decimal? SalePrice { get; private set; }

    public DateTimeOffset? SaleStartsOn { get; private set; }

    public DateTimeOffset? SaleEndsOn { get; private set; }

    public void SetSale(decimal? salePrice, DateTimeOffset? startsOn, DateTimeOffset? endsOn)
    {
        SalePrice = salePrice;
        SaleStartsOn = salePrice is null ? null : startsOn;
        SaleEndsOn = salePrice is null ? null : endsOn;
    }

    /// <summary>The sale price when one is set and <paramref name="now"/> is inside its window; otherwise null.</summary>
    public decimal? ActiveSalePrice(DateTimeOffset now) =>
        SalePrice is { } price && (SaleStartsOn is null || SaleStartsOn <= now) && (SaleEndsOn is null || now <= SaleEndsOn)
            ? price
            : null;
    public int SortOrder { get; private set; }
    public ProductVariantStatus Status { get; private set; }
    public bool IsVisible { get; private set; }
    public Guid? MembershipPlanId { get; private set; }
    public int LowStockThreshold { get; private set; }

    /// <summary>
    /// How a shortfall for this variant is sourced. Defaults to <see cref="FulfillmentMode.ManualOnly"/>
    /// for every variant, existing or new — see <see cref="FulfillmentMode"/> for the full contract.
    /// </summary>
    public FulfillmentMode FulfillmentMode { get; private set; }

    /// <summary>
    /// Remaining capacity for <see cref="FulfillmentMode.ChatDelivery"/> — how many more units of this
    /// variant can be sold before a customer is blocked, exactly like running out of digital codes
    /// would for a manual variant. Decremented at order fulfillment time. Meaningless for any other
    /// <see cref="FulfillmentMode"/>.
    /// </summary>
    public int? ManualDeliveryCapacity { get; private set; }
    /// <summary>
    /// Sanitized rich-text note shown to shoppers on the product page while this exact variant is selected (e.g.
    /// "Delivered within 5 minutes; needs a Turkish account"). Distinct from the private after-purchase guide kept in
    /// <c>ProductInstructions</c>, which only buyers can read.
    /// </summary>
    public string? DescriptionHtml { get; private set; }

    /// <summary>Show <see cref="DescriptionHtml"/> right under the options instead of behind "Important information".</summary>
    public bool ShowDescriptionInline { get; private set; }

    public void SetDescription(string? descriptionHtml, bool showInline)
    {
        DescriptionHtml = descriptionHtml;
        ShowDescriptionInline = descriptionHtml is not null && showInline;
    }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public IReadOnlyCollection<ProductVariantOption> SelectedOptions => _selectedOptions.AsReadOnly();

    public static ProductVariant Create(
        Guid productId,
        string sku,
        Guid? planId = null,
        decimal? priceOverride = null,
        decimal? comparePrice = null,
        int sortOrder = 0,
        int lowStockThreshold = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        return new ProductVariant(
            Guid.NewGuid(),
            productId,
            planId,
            sku.Trim().ToUpperInvariant(),
            priceOverride,
            comparePrice,
            sortOrder,
            lowStockThreshold);
    }

    public void Update(
        string sku,
        Guid? planId,
        decimal? priceOverride,
        decimal? comparePrice,
        int sortOrder,
        ProductVariantStatus status,
        bool isVisible,
        Guid? membershipPlanId,
        int lowStockThreshold,
        decimal? costPrice = null,
        decimal? memberPrice = null)
    {
        Sku = sku.Trim().ToUpperInvariant();
        PlanId = planId;
        PriceOverride = priceOverride;
        ComparePrice = comparePrice;
        SortOrder = sortOrder;
        Status = status;
        IsVisible = isVisible;
        MembershipPlanId = membershipPlanId;
        LowStockThreshold = lowStockThreshold;
        CostPrice = costPrice;
        MemberPrice = memberPrice;
    }

    /// <summary>Sets just this variant's own sale price (bulk price / margin edits), leaving every other field alone.</summary>
    public void SetPriceOverride(decimal? priceOverride) => PriceOverride = priceOverride;

    /// <summary>Sets (or, with null, clears) just the old price shown struck through next to the sale price.</summary>
    public void SetComparePrice(decimal? comparePrice) => ComparePrice = comparePrice;

    /// <summary>Sets just the (informational) cost price, leaving every other field untouched.</summary>
    public void SetCostPrice(decimal? costPrice) => CostPrice = costPrice;

    /// <summary>Sets just the Elite-member sale price, leaving every other field untouched.</summary>
    public void SetMemberPrice(decimal? memberPrice) => MemberPrice = memberPrice;

    /// <summary>
    /// Changes how a shortfall for this variant is sourced. Kept as its own method (not folded into
    /// <see cref="Update"/>) so callers can audit the before/after value distinctly — this is a
    /// business-critical, real-money-adjacent configuration change, not routine metadata editing.
    /// </summary>
    public FulfillmentMode SetFulfillmentMode(FulfillmentMode mode)
    {
        var previous = FulfillmentMode;
        FulfillmentMode = mode;
        return previous;
    }

    public void SetManualDeliveryCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ManualDeliveryCapacity = capacity;
    }

    /// <summary>
    /// Re-parents this variant under another product (a merge). The variant keeps its SKU, status, codes,
    /// fulfillment mode and prices; if it never had a price of its own it takes <paramref name="inheritedPrice"/>
    /// (the old product's base price) so its customer price does not silently change to the new parent's.
    /// The caller is responsible for assigning the new product's option via <see cref="SetOptions"/>.
    /// </summary>
    public void MoveToProduct(Guid productId, decimal? inheritedPrice, int sortOrder)
    {
        ProductId = productId;
        PriceOverride ??= inheritedPrice;
        SortOrder = sortOrder;
    }

    public void SetOptions(IEnumerable<Guid> optionIds)
    {
        _selectedOptions.Clear();
        foreach (var optionId in optionIds.Distinct())
        {
            _selectedOptions.Add(ProductVariantOption.Create(Id, optionId));
        }
    }

    public void Activate()
    {
        Status = ProductVariantStatus.Active;
        IsVisible = true;
    }

    public void Deactivate()
    {
        if (Status == ProductVariantStatus.Archived)
        {
            throw new InvalidOperationException("Archived variants cannot be deactivated.");
        }

        Status = ProductVariantStatus.Inactive;
        IsVisible = false;
    }

    /// <summary>
    /// The primary, reversible "take this variant off sale" action. Sets Status/IsVisible only —
    /// deliberately does NOT touch <see cref="IsDeleted"/>, so an archived variant stays reachable
    /// by every lookup that filters on it (e.g. <c>Activate()</c> can bring it back). Blocks new
    /// purchases and new inventory (see the Active-only checks at checkout and batch/code
    /// creation) while preserving every historical record that already points at this variant.
    /// </summary>
    public void Archive()
    {
        Status = ProductVariantStatus.Archived;
        IsVisible = false;
    }

    /// <summary>
    /// The permanent-delete tombstone. Deliberately irreversible in practice: once
    /// <see cref="IsDeleted"/> is true, the global soft-delete query filter excludes this row from
    /// every normal lookup (including the ones <c>Activate()</c>/<c>Update()</c> use), so there is
    /// no "un-delete" path — this must only be called after usage inspection has proven zero
    /// protected history remains for this variant.
    /// </summary>
    public void SoftDelete()
    {
        Archive();
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}
