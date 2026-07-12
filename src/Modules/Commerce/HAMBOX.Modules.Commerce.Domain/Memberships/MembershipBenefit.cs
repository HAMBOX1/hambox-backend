using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// Admin-configurable benefit attached to a membership plan.
/// </summary>
public sealed class MembershipBenefit : Entity
{
    private MembershipBenefit()
    {
    }

    private MembershipBenefit(
        Guid id,
        Guid planId,
        MembershipBenefitType benefitType,
        string value,
        string displayName,
        int sortOrder)
        : base(id)
    {
        PlanId = planId;
        BenefitType = benefitType;
        Value = value;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }

    public Guid PlanId { get; private set; }
    public MembershipBenefitType BenefitType { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public static MembershipBenefit Create(
        Guid planId,
        MembershipBenefitType benefitType,
        string value,
        string displayName,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new MembershipBenefit(Guid.NewGuid(), planId, benefitType, value.Trim(), displayName.Trim(), sortOrder);
    }
}
