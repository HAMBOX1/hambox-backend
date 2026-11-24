using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

/// <summary>
/// ELITE price slots (EX-03): a plan's "PriceSlots" benefit caps how many units a member can buy at the member price each
/// calendar month. A plan without the benefit has no cap. When the slots run out the customer simply pays the normal price —
/// checkout never fails because of them — and slots used by an order that fails or is cancelled are free again.
/// </summary>
public sealed class PriceSlotService(ICommerceDbContext db, ICatalogDbContext catalog, IMembershipEngine engine)
{
    public static string Period(DateTime utc) => utc.ToString("yyyy-MM");

    /// <summary>Slots left this month, or null when the member's plan has no slot limit (or they are not a member).</summary>
    public async Task<int?> GetRemainingAsync(string userId, CancellationToken cancellationToken)
    {
        var limit = await LimitAsync(userId, cancellationToken);
        if (limit is null)
        {
            return null;
        }

        var period = Period(DateTime.UtcNow);
        var used = await (from u in db.MemberPriceUsages.AsNoTracking()
                          join o in db.Orders.AsNoTracking() on u.OrderId equals o.Id
                          where u.UserId == userId && u.PeriodKey == period
                              && o.Status != OrderStatus.Failed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded
                          select (int?)u.Quantity).SumAsync(cancellationToken) ?? 0;
        return Math.Max(limit.Value - used, 0);
    }

    /// <summary>Records which lines of a freshly created order were bought at the member price (only for plans that limit slots).</summary>
    public async Task RecordAsync(Order order, CancellationToken cancellationToken)
    {
        if (await LimitAsync(order.UserId, cancellationToken) is null)
        {
            return;
        }

        var variantIds = order.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        if (variantIds.Count == 0)
        {
            return;
        }

        var memberPrices = await catalog.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id) && v.MemberPrice != null)
            .ToDictionaryAsync(v => v.Id, v => v.MemberPrice!.Value, cancellationToken);

        var period = Period(DateTime.UtcNow);
        foreach (var item in order.Items.Where(i => i.ProductVariantId.HasValue && memberPrices.TryGetValue(i.ProductVariantId!.Value, out var p) && i.UnitPrice == p))
        {
            db.MemberPriceUsages.Add(MemberPriceUsage.Create(order.UserId, order.Id, item.ProductVariantId!.Value, item.Quantity, period));
        }
    }

    private async Task<int?> LimitAsync(string userId, CancellationToken cancellationToken)
    {
        var snapshot = await engine.ResolveAsync(userId, cancellationToken);
        if (snapshot.SubscriptionId == Guid.Empty)
        {
            return null;
        }

        var value = snapshot.Benefits.FirstOrDefault(b => b.Type.Equals(nameof(MembershipBenefitType.PriceSlots), StringComparison.OrdinalIgnoreCase))?.Value;
        return int.TryParse(value, out var n) && n >= 0 ? n : null;
    }
}
