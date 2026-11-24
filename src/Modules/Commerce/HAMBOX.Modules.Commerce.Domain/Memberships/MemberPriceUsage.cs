using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// A unit of an ELITE "price slot" used by an order: how many units of one variant were bought at the member price in a calendar
/// month. The slot count of a period is the sum over orders that are still alive, so a failed, cancelled or refunded order
/// frees its slots without any explicit release step.
/// </summary>
public sealed class MemberPriceUsage : Entity
{
    private MemberPriceUsage()
    {
    }

    private MemberPriceUsage(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public Guid OrderId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>The calendar month ("yyyy-MM") the order was placed in.</summary>
    public string PeriodKey { get; private set; } = string.Empty;

    public static MemberPriceUsage Create(string userId, Guid orderId, Guid variantId, int quantity, string periodKey) =>
        new(Guid.NewGuid()) { UserId = userId, OrderId = orderId, ProductVariantId = variantId, Quantity = quantity, PeriodKey = periodKey };
}
