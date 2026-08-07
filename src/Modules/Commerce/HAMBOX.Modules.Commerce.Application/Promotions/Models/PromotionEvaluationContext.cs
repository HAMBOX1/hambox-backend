using HAMBOX.Modules.Commerce.Application.Memberships.Models;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Models;

/// <summary>
/// Context passed to the promotion engine during cart or checkout evaluation.
/// </summary>
public sealed record PromotionEvaluationContext(
    IReadOnlyList<PromotionCartLine> Lines,
    string? UserId,
    string? CountryCode,
    bool IsAuthenticated,
    bool IsFirstPurchase,
    string? AppliedCouponCode,
    MembershipSnapshot Membership,
    DateTime UtcNow);
