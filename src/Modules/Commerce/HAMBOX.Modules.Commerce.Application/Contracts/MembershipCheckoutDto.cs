namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>
/// Membership checkout review payload.
/// </summary>
public sealed record MembershipCheckoutPreviewDto(
    Guid PlanId,
    string PlanName,
    string Action,
    decimal Price,
    int DurationDays,
    string? BadgeLabel,
    int BenefitCount,
    bool RequiresPayment,
    string? CurrentPlanName);

/// <summary>
/// Membership checkout request body.
/// </summary>
public sealed record MembershipCheckoutRequest(
    Guid PlanId,
    string Action,
    string Email,
    string Country,
    string PaymentMethod,
    string? CouponCode = null);
