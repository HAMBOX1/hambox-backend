using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>Customer activity for the admin Customers screen: orders and spend, membership, points balance and referral code.</summary>
internal sealed class CustomerActivityReader(ICommerceDbContext db) : ICustomerActivityReader
{
    public async Task<IReadOnlyDictionary<string, CustomerActivity>> GetActivityAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, CustomerActivity>();
        }

        var ids = userIds.ToList();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Paid = g.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                Spend = g.Where(o => o.PaymentStatus == PaymentStatus.Paid).Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                Last = g.Max(o => (DateTimeOffset?)o.CreatedOnUtc),
            })
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        // The purchase-points ledger is a newer table: where its migration has not been applied yet there is simply no
        // balance to show (SQL error 208 = invalid object name), so the Customers screen still loads.
        Dictionary<string, int> points;
        try
        {
            points = await db.PointsTransactions.AsNoTracking()
                .Where(p => ids.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, Balance = g.Sum(p => p.Points) })
                .ToDictionaryAsync(x => x.UserId, x => x.Balance, cancellationToken);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
        {
            points = [];
        }

        var referrals = await db.ReferralProfiles.AsNoTracking()
            .Where(r => ids.Contains(r.UserId))
            .Select(r => new { r.UserId, r.ReferralCode, r.Tier })
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        // The most relevant subscription per customer: an active one first, otherwise the latest.
        var subscriptions = await (
            from s in db.MembershipSubscriptions.AsNoTracking()
            join p in db.MembershipPlans.AsNoTracking() on s.PlanId equals p.Id
            where ids.Contains(s.UserId)
            select new { s.UserId, PlanName = p.Name, s.Status, s.ExpiresOnUtc })
            .ToListAsync(cancellationToken);
        var membership = subscriptions
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.Status == MembershipSubscriptionStatus.Active).ThenByDescending(s => s.ExpiresOnUtc).First());

        var result = new Dictionary<string, CustomerActivity>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            orders.TryGetValue(id, out var o);
            membership.TryGetValue(id, out var m);
            referrals.TryGetValue(id, out var r);
            var balance = points.GetValueOrDefault(id);
            if (o is null && m is null && r is null && balance == 0)
            {
                continue;
            }

            result[id] = new CustomerActivity(
                o?.Count ?? 0, o?.Paid ?? 0, o?.Spend ?? 0m, o?.Last,
                m?.PlanName, m?.Status.ToString(), m?.ExpiresOnUtc,
                balance, r?.ReferralCode, string.IsNullOrWhiteSpace(r?.Tier) ? null : r!.Tier);
        }

        return result;
    }

    public async Task<IReadOnlySet<string>> GetActiveMemberUserIdsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var ids = await db.MembershipSubscriptions.AsNoTracking()
            .Where(s => s.Status == MembershipSubscriptionStatus.Active && s.ExpiresOnUtc > now)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
