namespace HAMBOX.Modules.Commerce.Domain.Promotions;

public enum PromotionConditionType
{
    MinOrderAmount = 0,
    MaxDiscountAmount = 1,
    UsageLimit = 2,
    PerUserLimit = 3,
    FirstPurchaseOnly = 4,
    MembershipRequired = 5,
    CountryCode = 6,
}
