using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Loyalty;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Loyalty;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class LoyaltyRankServiceTests
{
    private const string User = "u1";

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;
    private readonly FakeCommerceSettingsProvider _settings = new() { Features = new FeatureCenterSettingsPayload(LoyaltyRanks: true) };

    private LoyaltyRankService Service => new(_db, _settings, NullLogger<LoyaltyRankService>.Instance);

    private async Task BuyAsync(decimal amount)
    {
        var order = Order.Create(User, $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "Development", amount, 0m, 0m, amount, []);
        order.RecordPayment("Development", Guid.NewGuid().ToString());
        order.Complete();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
    }

    private async Task<string> RankAsync() =>
        (await _db.CustomerLoyaltyRanks.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == User))?.RankKey ?? LoyaltyRankKeys.Normal;

    [Fact]
    public async Task Disabled_feature_is_inert()
    {
        _settings.Features = new FeatureCenterSettingsPayload();
        await BuyAsync(5000m);

        await Service.EvaluateAsync(User, default);

        Assert.Empty(_db.CustomerLoyaltyRanks);
    }

    [Fact]
    public async Task Rank_follows_spend_thresholds_and_records_history()
    {
        await BuyAsync(120m);
        await Service.EvaluateAsync(User, default);
        Assert.Equal(LoyaltyRankKeys.Bronze, await RankAsync());

        await BuyAsync(1500m);
        await Service.EvaluateAsync(User, default);
        Assert.Equal(LoyaltyRankKeys.Gold, await RankAsync());
        Assert.Equal(2, await _db.LoyaltyRankHistory.CountAsync());
    }

    [Fact]
    public async Task Spending_stars_never_lowers_the_rank()
    {
        await BuyAsync(600m);
        _db.PointsTransactions.Add(PointsTransaction.Credit(User, PointsTransactionType.Bonus, 800, "b", "k1"));
        _db.PointsTransactions.Add(PointsTransaction.Debit(User, PointsTransactionType.Redeemed, 800, "r", "k2"));
        await _db.SaveChangesAsync();

        await Service.EvaluateAsync(User, default);

        Assert.Equal(LoyaltyRankKeys.Silver, await RankAsync());
    }

    [Fact]
    public async Task Manual_rank_is_pinned_until_released()
    {
        await BuyAsync(10m);
        Assert.True(await Service.SetManualAsync(User, LoyaltyRankKeys.Diamond, "VIP", "staff", default));

        await BuyAsync(10m);
        await Service.EvaluateAsync(User, default);
        Assert.Equal(LoyaltyRankKeys.Diamond, await RankAsync());

        await Service.SetManualAsync(User, null, "released", "staff", default);
        Assert.Equal(LoyaltyRankKeys.Normal, await RankAsync());
    }

    [Fact]
    public async Task Refund_keeps_the_rank_unless_downgrade_is_allowed()
    {
        await BuyAsync(600m);
        await Service.EvaluateAsync(User, default);
        var order = await _db.Orders.FirstAsync();
        order.Refund();
        await _db.SaveChangesAsync();

        await Service.EvaluateAsync(User, default);
        Assert.Equal(LoyaltyRankKeys.Silver, await RankAsync());

        _settings.Loyalty = new LoyaltySettingsPayload(AllowDowngrade: true);
        await Service.EvaluateAsync(User, default);
        Assert.Equal(LoyaltyRankKeys.Normal, await RankAsync());
    }

    [Fact]
    public async Task Status_reports_next_rank_and_progress()
    {
        await BuyAsync(300m);

        var status = await Service.GetStatusAsync(User, default);

        Assert.Equal(LoyaltyRankKeys.Bronze, status.Current.Key);
        Assert.Equal(LoyaltyRankKeys.Silver, status.Next!.Key);
        Assert.Equal(200m, status.Remaining);
        Assert.Equal(50, status.ProgressPercent);
    }
}
