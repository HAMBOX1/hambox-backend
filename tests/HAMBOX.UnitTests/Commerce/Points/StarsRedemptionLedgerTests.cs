using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class StarsRedemptionLedgerTests
{
    private const string User = "u1";

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;

    private StarsLedgerService Ledger => new(_db);

    private async Task FundAsync(int stars)
    {
        _db.PointsTransactions.Add(PointsTransaction.Credit(User, PointsTransactionType.Bonus, stars, "seed", $"seed:{Guid.NewGuid():N}"));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Reserve_holds_stars_then_settle_makes_the_spend_final()
    {
        await FundAsync(800);
        var orderId = Guid.NewGuid();

        Assert.True(await Ledger.ReserveForOrderAsync(User, 800, orderId, "ORD-1", "reward", default));
        await _db.SaveChangesAsync();
        var held = await Ledger.GetBalanceAsync(User, default);
        Assert.Equal(0, held.Available);
        Assert.Equal(800, held.Reserved);

        await Ledger.SettleForOrderAsync(orderId, default);
        var settled = await Ledger.GetBalanceAsync(User, default);
        Assert.Equal(0, settled.Available);
        Assert.Equal(0, settled.Reserved);
    }

    [Fact]
    public async Task Reserve_refuses_when_the_balance_cannot_cover_it_and_never_double_reserves()
    {
        await FundAsync(500);

        Assert.False(await Ledger.ReserveForOrderAsync(User, 800, Guid.NewGuid(), "ORD-1", "reward", default));

        var orderId = Guid.NewGuid();
        Assert.True(await Ledger.ReserveForOrderAsync(User, 400, orderId, "ORD-2", "reward", default));
        await _db.SaveChangesAsync();
        Assert.True(await Ledger.ReserveForOrderAsync(User, 400, orderId, "ORD-2", "reward", default));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.PointsTransactions.CountAsync(t => t.Type == PointsTransactionType.Redeemed));
        Assert.Equal(100, (await Ledger.GetBalanceAsync(User, default)).Available);
    }

    [Fact]
    public async Task Release_voids_a_reserved_spend_and_refunds_a_settled_one_exactly_once()
    {
        await FundAsync(300);
        var failed = Guid.NewGuid();
        await Ledger.ReserveForOrderAsync(User, 100, failed, "ORD-1", "reward", default);
        await _db.SaveChangesAsync();
        await Ledger.ReleaseForOrderAsync(failed, User, default);
        Assert.Equal(300, (await Ledger.GetBalanceAsync(User, default)).Available);

        var refunded = Guid.NewGuid();
        await Ledger.ReserveForOrderAsync(User, 100, refunded, "ORD-2", "reward", default);
        await _db.SaveChangesAsync();
        await Ledger.SettleForOrderAsync(refunded, default);
        Assert.Equal(200, (await Ledger.GetBalanceAsync(User, default)).Available);

        await Ledger.ReleaseForOrderAsync(refunded, User, default);
        await Ledger.ReleaseForOrderAsync(refunded, User, default);
        Assert.Equal(300, (await Ledger.GetBalanceAsync(User, default)).Available);
    }

    [Fact]
    public void Calendar_month_plans_expire_on_the_same_clock_time_not_after_thirty_days()
    {
        var plan = MembershipPlan.Create("ELITE", "elite", null, 10m, 30, 0);
        plan.SetDurationMonths(1);
        var start = new DateTime(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 12, 13, 15, 0, 0, DateTimeKind.Utc), plan.ExpiryFrom(start));

        var subscription = MembershipSubscription.Create(User, plan.Id, 30, false, 1);
        Assert.Equal(subscription.StartsOnUtc.AddMonths(1), subscription.ExpiresOnUtc);
        Assert.False(subscription.IsActiveAt(subscription.ExpiresOnUtc));
        Assert.True(subscription.IsActiveAt(subscription.ExpiresOnUtc.AddSeconds(-1)));
    }

    [Fact]
    public void Early_renewal_extends_from_the_remaining_time_not_from_today()
    {
        var subscription = MembershipSubscription.Create(User, Guid.NewGuid(), 30, false, 1);
        var before = subscription.ExpiresOnUtc;

        subscription.Renew(30, 1);

        Assert.Equal(before.AddMonths(1), subscription.ExpiresOnUtc);
    }
}
