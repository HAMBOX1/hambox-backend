using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// Configurable membership plan (Free, Silver, Gold, etc.).
/// </summary>
public sealed class MembershipPlan : AggregateRoot
{
    private readonly List<MembershipBenefit> _benefits = [];
    private readonly List<MembershipPlanProductAccess> _exclusiveProductAccess = [];

    private MembershipPlan()
    {
    }

    private MembershipPlan(
        Guid id,
        string name,
        string slug,
        string? description,
        decimal price,
        int durationDays,
        int sortOrder,
        string? badgeLabel,
        string? themeKey,
        bool isDefault)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Description = description;
        Price = price;
        DurationDays = durationDays;
        SortOrder = sortOrder;
        BadgeLabel = badgeLabel;
        ThemeKey = themeKey;
        IsDefault = isDefault;
        Status = MembershipPlanStatus.Draft;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int DurationDays { get; private set; }
    public int SortOrder { get; private set; }
    public string? BadgeLabel { get; private set; }
    public string? ThemeKey { get; private set; }
    public bool IsDefault { get; private set; }
    public MembershipPlanStatus Status { get; private set; }

    public IReadOnlyCollection<MembershipBenefit> Benefits => _benefits.AsReadOnly();
    public IReadOnlyCollection<MembershipPlanProductAccess> ExclusiveProductAccess => _exclusiveProductAccess.AsReadOnly();

    public static MembershipPlan Create(
        string name,
        string slug,
        string? description,
        decimal price,
        int durationDays,
        int sortOrder,
        string? badgeLabel = null,
        string? themeKey = null,
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (durationDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationDays));
        }

        return new MembershipPlan(
            Guid.NewGuid(),
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            description?.Trim(),
            price,
            durationDays,
            sortOrder,
            badgeLabel?.Trim(),
            themeKey?.Trim(),
            isDefault);
    }

    public void Update(
        string name,
        string slug,
        string? description,
        decimal price,
        int durationDays,
        int sortOrder,
        string? badgeLabel,
        string? themeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = description?.Trim();
        Price = price;
        DurationDays = durationDays;
        SortOrder = sortOrder;
        BadgeLabel = badgeLabel?.Trim();
        ThemeKey = themeKey?.Trim();
    }

    public void Activate() => Status = MembershipPlanStatus.Active;

    public void Archive()
    {
        IsDefault = false;
        Status = MembershipPlanStatus.Archived;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public MembershipPlan Duplicate(string newName, string newSlug)
    {
        var copy = Create(newName, newSlug, Description, Price, DurationDays, SortOrder, BadgeLabel, ThemeKey);
        foreach (var benefit in _benefits)
        {
            copy.AddBenefit(benefit.BenefitType, benefit.Value, benefit.DisplayName, benefit.SortOrder);
        }

        copy.SetExclusiveProducts(_exclusiveProductAccess.Select(a => a.ProductId));

        return copy;
    }

    public void ReplaceBenefits(IEnumerable<(MembershipBenefitType Type, string Value, string DisplayName, int SortOrder)> benefits)
    {
        _benefits.Clear();
        foreach (var (type, value, displayName, sortOrder) in benefits)
        {
            AddBenefit(type, value, displayName, sortOrder);
        }
    }

    public MembershipBenefit AddBenefit(
        MembershipBenefitType benefitType,
        string value,
        string displayName,
        int sortOrder)
    {
        var benefit = MembershipBenefit.Create(Id, benefitType, value, displayName, sortOrder);
        _benefits.Add(benefit);
        return benefit;
    }

    /// <summary>
    /// Replaces the full set of products exclusively unlocked for this plan's members
    /// (backs the ExclusiveProducts benefit). Duplicates are ignored.
    /// </summary>
    public void SetExclusiveProducts(IEnumerable<Guid> productIds)
    {
        var distinctIds = productIds.Distinct().ToList();
        _exclusiveProductAccess.RemoveAll(a => !distinctIds.Contains(a.ProductId));

        var existingIds = _exclusiveProductAccess.Select(a => a.ProductId).ToHashSet();
        foreach (var productId in distinctIds.Where(id => !existingIds.Contains(id)))
        {
            _exclusiveProductAccess.Add(MembershipPlanProductAccess.Create(Id, productId));
        }
    }
}
