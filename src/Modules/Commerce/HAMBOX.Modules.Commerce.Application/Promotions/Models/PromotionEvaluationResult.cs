namespace HAMBOX.Modules.Commerce.Application.Promotions.Models;

/// <summary>
/// Result of promotion engine evaluation including totals.
/// </summary>
public sealed record PromotionEvaluationResult(
    decimal Subtotal,
    decimal TotalDiscount,
    decimal Tax,
    decimal Total,
    int ItemCount,
    IReadOnlyList<AppliedPromotionDto> AppliedPromotions,
    IReadOnlyList<string> ValidationErrors)
{
    public static PromotionEvaluationResult Empty =>
        new(0m, 0m, 0m, 0m, 0, [], []);
}
