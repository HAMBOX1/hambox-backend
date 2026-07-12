namespace HAMBOX.Modules.Commerce.Domain.Memberships;

public enum MembershipPlanStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
}

public enum MembershipSubscriptionStatus
{
    Active = 0,
    Expired = 1,
    Cancelled = 2,
    PendingRenewal = 3,
    PendingPayment = 4,
}

public enum MembershipBenefitType
{
    DiscountPercentage = 0,
    ReferralMultiplier = 1,
    PrioritySupport = 2,
    ExclusiveProducts = 3,
    EarlyAccess = 4,
    MaxPurchasesPerMonth = 5,
    Badge = 6,
    ThemeUnlock = 7,
    FeatureFlag = 8,
}

public enum MembershipHistoryAction
{
    Assigned = 0,
    Renewed = 1,
    Upgraded = 2,
    Downgraded = 3,
    Cancelled = 4,
    Expired = 5,
}

public enum MembershipTransactionStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3,
}

public enum MembershipAuditAction
{
    PlanCreated = 0,
    PlanUpdated = 1,
    PlanDeleted = 2,
    MembershipAssigned = 3,
    MembershipRenewed = 4,
    MembershipCancelled = 5,
    MembershipUpgraded = 6,
    MembershipDowngraded = 7,
    BenefitChanged = 8,
}
