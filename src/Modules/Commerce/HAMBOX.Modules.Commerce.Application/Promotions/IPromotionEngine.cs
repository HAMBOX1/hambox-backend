using HAMBOX.Modules.Commerce.Application.Promotions.Models;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

/// <summary>
/// Evaluates promotions for a cart or checkout without duplicating pricing in handlers.
/// </summary>
public interface IPromotionEngine
{
  Task<PromotionEvaluationResult> EvaluateAsync(
      PromotionEvaluationContext context,
      CancellationToken cancellationToken = default);

  Task<(bool IsValid, string? ErrorMessage, Guid? PromotionId, Guid? CouponCodeId)> ValidateCouponAsync(
      string couponCode,
      PromotionEvaluationContext context,
      CancellationToken cancellationToken = default);
}
