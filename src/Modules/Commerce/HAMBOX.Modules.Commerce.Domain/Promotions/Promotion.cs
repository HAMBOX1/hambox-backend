using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>
/// Aggregate root for a marketing promotion.
/// </summary>
public sealed class Promotion : AggregateRoot, ISoftDeletable
{
    private readonly List<PromotionCondition> _conditions = [];
    private readonly List<PromotionTarget> _targets = [];
    private readonly List<CouponCode> _couponCodes = [];

    private Promotion()
    {
    }

    private Promotion(
        Guid id,
        string name,
        string? description,
        PromotionType type,
        DiscountType discountType,
        decimal discountValue,
        PromotionStatus status,
        bool isPublished,
        DateTime? startDateUtc,
        DateTime? endDateUtc)
        : base(id)
    {
        Name = name;
        Description = description;
        Type = type;
        DiscountType = discountType;
        DiscountValue = discountValue;
        Status = status;
        IsPublished = isPublished;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public PromotionType Type { get; private set; }
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public PromotionStatus Status { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public int TotalRedemptions { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    public IReadOnlyCollection<PromotionCondition> Conditions => _conditions.AsReadOnly();
    public IReadOnlyCollection<PromotionTarget> Targets => _targets.AsReadOnly();
    public IReadOnlyCollection<CouponCode> CouponCodes => _couponCodes.AsReadOnly();

    public static Promotion Create(
        string name,
        string? description,
        PromotionType type,
        DiscountType discountType,
        decimal discountValue,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (discountValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountValue), "Discount value must be positive.");
        }

        return new Promotion(
            Guid.NewGuid(),
            name.Trim(),
            description?.Trim(),
            type,
            discountType,
            discountValue,
            PromotionStatus.Draft,
            isPublished: false,
            startDateUtc,
            endDateUtc);
    }

    public void UpdateDetails(
        string name,
        string? description,
        DiscountType discountType,
        decimal discountValue,
        DateTime? startDateUtc,
        DateTime? endDateUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (discountValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountValue), "Discount value must be positive.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        DiscountType = discountType;
        DiscountValue = discountValue;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
    }

    public void SetStatus(PromotionStatus status) => Status = status;

    public void Publish()
    {
        if (Status is PromotionStatus.Archived)
        {
            throw new InvalidOperationException("Archived promotions cannot be published.");
        }

        IsPublished = true;
        Status = PromotionStatus.Active;
    }

    public void Unpublish()
    {
        IsPublished = false;
        if (Status == PromotionStatus.Active)
        {
            Status = PromotionStatus.Inactive;
        }
    }

    public void Activate()
    {
        if (Status == PromotionStatus.Archived)
        {
            throw new InvalidOperationException("Archived promotions cannot be activated.");
        }

        Status = PromotionStatus.Active;
        IsPublished = true;
    }

    public void Archive()
    {
        IsPublished = false;
        Status = PromotionStatus.Archived;
    }

    public Promotion Duplicate(string newName)
    {
        var copy = Create(newName, Description, Type, DiscountType, DiscountValue, StartDateUtc, EndDateUtc);
        foreach (var condition in _conditions)
        {
            copy.AddCondition(condition.ConditionType, condition.Value);
        }

        foreach (var target in _targets)
        {
            copy.AddTarget(target.TargetType, target.TargetId);
        }

        return copy;
    }

    public void ReplaceConditions(IEnumerable<(PromotionConditionType Type, string Value)> conditions)
    {
        _conditions.Clear();
        foreach (var (type, value) in conditions)
        {
            AddCondition(type, value);
        }
    }

    public void ReplaceTargets(IEnumerable<(PromotionTargetType Type, Guid TargetId)> targets)
    {
        _targets.Clear();
        foreach (var (type, targetId) in targets)
        {
            AddTarget(type, targetId);
        }
    }

    public PromotionCondition AddCondition(PromotionConditionType conditionType, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var condition = PromotionCondition.Create(Id, conditionType, value);
        _conditions.Add(condition);
        return condition;
    }

    public PromotionTarget AddTarget(PromotionTargetType targetType, Guid targetId)
    {
        var target = PromotionTarget.Create(Id, targetType, targetId);
        _targets.Add(target);
        return target;
    }

    public CouponCode AddCouponCode(
        string code,
        bool isSingleUse,
        int? maxUses,
        int? perUserMaxUses,
        DateTime? expiresOnUtc)
    {
        var coupon = CouponCode.Create(Id, code, isSingleUse, maxUses, perUserMaxUses, expiresOnUtc);
        _couponCodes.Add(coupon);
        return coupon;
    }

    public void RecordRedemption() => TotalRedemptions++;

    public bool IsActiveAt(DateTime utcNow)
    {
        if (Status != PromotionStatus.Active || !IsPublished)
        {
            return false;
        }

        if (StartDateUtc.HasValue && utcNow < StartDateUtc.Value)
        {
            return false;
        }

        if (EndDateUtc.HasValue && utcNow > EndDateUtc.Value)
        {
            return false;
        }

        return true;
    }

    public decimal? GetConditionDecimal(PromotionConditionType type) =>
        _conditions.FirstOrDefault(c => c.ConditionType == type) is { } c
            ? decimal.TryParse(c.Value, out var value) ? value : null
            : null;

    public int? GetConditionInt(PromotionConditionType type) =>
        _conditions.FirstOrDefault(c => c.ConditionType == type) is { } c
            ? int.TryParse(c.Value, out var value) ? value : null
            : null;

    public bool HasCondition(PromotionConditionType type) =>
        _conditions.Any(c => c.ConditionType == type);

    public IReadOnlyCollection<Guid> GetTargetIds(PromotionTargetType type) =>
        _targets.Where(t => t.TargetType == type).Select(t => t.TargetId).ToList();
}
