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
    }

    public Guid ProductId { get; private set; }
    public Guid? PlanId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public decimal? PriceOverride { get; private set; }
    public decimal? ComparePrice { get; private set; }
    public int SortOrder { get; private set; }
    public ProductVariantStatus Status { get; private set; }
    public bool IsVisible { get; private set; }
    public Guid? MembershipPlanId { get; private set; }
    public int LowStockThreshold { get; private set; }
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
        int lowStockThreshold)
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
