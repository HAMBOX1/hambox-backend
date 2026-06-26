using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Domain.Carts;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Calculates shopping cart totals including member discounts and tax.
/// </summary>
internal static class CartTotalsCalculator
{
    private const decimal MemberDiscountRate = 0.10m;
    private const decimal TaxRate = 0.05m;

    /// <summary>
    /// Calculates totals for the provided cart items.
    /// </summary>
    public static CartTotalsDto Calculate(IEnumerable<CartItem> items, bool isAuthenticated)
    {
        var itemList = items.ToList();
        var subtotal = itemList.Sum(i => i.UnitPrice * i.Quantity);
        var memberDiscount = isAuthenticated ? decimal.Round(subtotal * MemberDiscountRate, 2) : 0m;
        var taxableAmount = subtotal - memberDiscount;
        var tax = decimal.Round(taxableAmount * TaxRate, 2);
        var total = taxableAmount + tax;
        var itemCount = itemList.Sum(i => i.Quantity);

        return new CartTotalsDto(subtotal, memberDiscount, tax, total, itemCount);
    }

    /// <summary>
    /// Calculates order amounts using the same rules as the cart.
    /// </summary>
    public static (decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount) CalculateOrderAmounts(
        IEnumerable<CartItem> items,
        bool isAuthenticated)
    {
        var totals = Calculate(items, isAuthenticated);
        return (totals.Subtotal, totals.MemberDiscount, totals.Tax, totals.Total);
    }
}
