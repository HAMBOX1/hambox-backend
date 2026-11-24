using System.Globalization;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Loyalty;

public sealed record TabularReport(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Real database reports for the loyalty programme (AD-02): every figure is a live aggregate of the ledger, orders and
/// subscriptions — nothing is estimated or demo data. A period is applied wherever it makes sense; rows are capped so a
/// single download stays a manageable size.
/// </summary>
public sealed class LoyaltyReportService(ICommerceDbContext db)
{
    public const int MaxRows = 20_000;

    public static readonly IReadOnlyList<string> Kinds = ["stars", "stars-summary", "subscriptions", "referrals", "redemptions", "business-orders"];

    private static string Date(DateTimeOffset? d) => d?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Date(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    public async Task<TabularReport?> BuildAsync(string kind, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var start = from ?? DateTimeOffset.MinValue;
        var end = to ?? DateTimeOffset.MaxValue;

        switch (kind.ToLowerInvariant())
        {
            case "stars":
            {
                var rows = await db.PointsTransactions.AsNoTracking()
                    .Where(t => t.OccurredOnUtc >= start && t.OccurredOnUtc <= end)
                    .OrderByDescending(t => t.OccurredOnUtc).Take(MaxRows).ToListAsync(cancellationToken);
                return new TabularReport("stars", ["Date", "CustomerId", "Source", "Stars", "Status", "Order", "AvailableOn", "ExpiresOn", "Description"],
                    rows.Select(t => (IReadOnlyList<string>)[
                        Date(t.OccurredOnUtc), t.UserId, t.Type.ToString(), t.Points.ToString(CultureInfo.InvariantCulture), t.Status.ToString(),
                        t.OrderNumber ?? string.Empty, Date(t.AvailableOnUtc), Date(t.ExpiresOnUtc), t.Description]).ToList());
            }

            case "stars-summary":
            {
                var groups = await db.PointsTransactions.AsNoTracking()
                    .Where(t => t.OccurredOnUtc >= start && t.OccurredOnUtc <= end)
                    .GroupBy(t => new { t.Type, t.Status })
                    .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count(), Stars = g.Sum(t => t.Points) })
                    .ToListAsync(cancellationToken);
                return new TabularReport("stars-summary", ["Source", "Status", "Entries", "Stars"],
                    groups.OrderBy(g => g.Type).ThenBy(g => g.Status).Select(g => (IReadOnlyList<string>)[
                        g.Type.ToString(), g.Status.ToString(), g.Count.ToString(CultureInfo.InvariantCulture), g.Stars.ToString(CultureInfo.InvariantCulture)]).ToList());
            }

            case "subscriptions":
            {
                var now = DateTime.UtcNow;
                var rows = await (from s in db.MembershipSubscriptions.AsNoTracking()
                                  join p in db.MembershipPlans.AsNoTracking() on s.PlanId equals p.Id
                                  where s.StartsOnUtc <= end.UtcDateTime || end == DateTimeOffset.MaxValue
                                  orderby s.ExpiresOnUtc
                                  select new { s.UserId, Plan = p.Name, s.Status, s.StartsOnUtc, s.ExpiresOnUtc, s.AutoRenew })
                    .Take(MaxRows).ToListAsync(cancellationToken);
                return new TabularReport("subscriptions", ["CustomerId", "Plan", "Status", "StartsOn", "ExpiresOn", "DaysLeft", "Phase"],
                    rows.Select(s =>
                    {
                        var left = (s.ExpiresOnUtc - now).TotalDays;
                        var phase = s.Status != MembershipSubscriptionStatus.Active ? s.Status.ToString()
                            : left <= 0 ? "Expired" : left <= 7 ? "ExpiringSoon" : "Active";
                        return (IReadOnlyList<string>)[s.UserId, s.Plan, s.Status.ToString(), Date(s.StartsOnUtc), Date(s.ExpiresOnUtc), Math.Max((int)Math.Ceiling(left), 0).ToString(CultureInfo.InvariantCulture), phase];
                    }).ToList());
            }

            case "referrals":
            {
                var rows = await db.ReferralHistoryEntries.AsNoTracking()
                    .Where(h => h.CreatedOnUtc >= start && h.CreatedOnUtc <= end)
                    .OrderByDescending(h => h.CreatedOnUtc).Take(MaxRows).ToListAsync(cancellationToken);
                return new TabularReport("referrals", ["Date", "ReferrerId", "ReferredId", "Status", "Stars", "HeldForReview"],
                    rows.Select(h => (IReadOnlyList<string>)[
                        Date(h.CreatedOnUtc), h.ReferrerUserId, h.ReferredUserId, h.Status.ToString(),
                        h.PointsEarned.ToString(CultureInfo.InvariantCulture), h.IsHeldForReview ? "TRUE" : "FALSE"]).ToList());
            }

            case "redemptions":
            {
                var rows = await db.RewardRedemptions.AsNoTracking()
                    .Where(r => r.RedeemedOnUtc >= start && r.RedeemedOnUtc <= end)
                    .OrderByDescending(r => r.RedeemedOnUtc).Take(MaxRows).ToListAsync(cancellationToken);
                var orders = rows.Select(r => r.OrderId).ToList();
                var status = await db.Orders.AsNoTracking().Where(o => orders.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => new { o.OrderNumber, o.Status }, cancellationToken);
                return new TabularReport("redemptions", ["Date", "CustomerId", "Source", "Stars", "Order", "OrderStatus"],
                    rows.Select(r => (IReadOnlyList<string>)[
                        Date(r.RedeemedOnUtc), r.UserId, r.Source.ToString(), r.StarsSpent.ToString(CultureInfo.InvariantCulture),
                        status.TryGetValue(r.OrderId, out var o) ? o.OrderNumber : string.Empty, status.TryGetValue(r.OrderId, out var o2) ? o2.Status.ToString() : string.Empty]).ToList());
            }

            case "business-orders":
            {
                var rows = await db.Orders.AsNoTracking()
                    .Where(o => o.PaymentMethod == "b2b" && o.CreatedOnUtc >= start && o.CreatedOnUtc <= end)
                    .OrderByDescending(o => o.CreatedOnUtc).Take(MaxRows).ToListAsync(cancellationToken);
                return new TabularReport("business-orders", ["Date", "Order", "CustomerId", "TotalUsd", "PaymentStatus", "OrderStatus"],
                    rows.Select(o => (IReadOnlyList<string>)[
                        Date(o.CreatedOnUtc), o.OrderNumber, o.UserId, o.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture), o.PaymentStatus.ToString(), o.Status.ToString()]).ToList());
            }

            default:
                return null;
        }
    }
}
