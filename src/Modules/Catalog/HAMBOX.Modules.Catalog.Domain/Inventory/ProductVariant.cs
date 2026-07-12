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

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
        Status = ProductVariantStatus.Archived;
        IsVisible = false;
    }
}
