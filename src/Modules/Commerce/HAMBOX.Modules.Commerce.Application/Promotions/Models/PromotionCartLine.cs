namespace HAMBOX.Modules.Commerce.Application.Promotions.Models;

/// <summary>
/// A cart line used during promotion evaluation.
/// </summary>
public sealed record PromotionCartLine(
    Guid ProductId,
    Guid? CategoryId,
    int Quantity,
    decimal UnitPrice)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
