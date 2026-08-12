using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>No-discount evaluator: H1's tests are about variant/inventory branching in checkout,
/// not pricing, so a flat subtotal-as-total result keeps the fakes minimal.</summary>
internal sealed class FakePromotionEngine : IPromotionEngine
{
    public Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var subtotal = context.Lines.Sum(l => l.UnitPrice * l.Quantity);
        var itemCount = context.Lines.Sum(l => l.Quantity);
        return Task.FromResult(new PromotionEvaluationResult(subtotal, 0m, 0m, subtotal, itemCount, [], []));
    }

    public Task<(bool IsValid, string? ErrorMessage, Guid? PromotionId, Guid? CouponCodeId)> ValidateCouponAsync(
        string couponCode, PromotionEvaluationContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult<(bool, string?, Guid?, Guid?)>((false, "Not supported in this test double.", null, null));
}
