using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class StarsCheckoutServiceTests
{
    private const string User = "u1";

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;
    private readonly FakeCommerceSettingsProvider _settings = new()
    {
        Features = new FeatureCenterSettingsPayload(StarsShop: true),
        PurchasePoints = new PurchasePointsSettingsPayload(true, 1m, PurchasePointsAmountBasis.NetOfTax, RedeemStarsPerUsd: 100m, RedeemMaxPercent: 50),
    };

    private async Task<StarsCheckoutService> ServiceAsync(int balance)
    {
        _db.PointsTransactions.Add(PointsTransaction.Credit(User, PointsTransactionType.Bonus, balance, "seed", "seed"));
        await _db.SaveChangesAsync();
        return new StarsCheckoutService(_settings, new FakeCurrentUserService(User), new StarsLedgerService(_db));
    }

    [Fact]
    public async Task Five_hundred_stars_buy_five_dollars_off_at_a_hundred_per_dollar()
    {
        var quote = await (await ServiceAsync(500)).QuoteAsync(500, 40m, default);

        Assert.Equal(500, quote.Stars);
        Assert.Equal(5m, quote.DiscountUsd);
    }

    [Fact]
    public async Task Discount_is_capped_by_the_max_share_of_the_order()
    {
        var quote = await (await ServiceAsync(5000)).QuoteAsync(5000, 10m, default);

        Assert.Equal(5m, quote.DiscountUsd);
        Assert.Equal(500, quote.Stars);
    }

    [Fact]
    public async Task Never_covers_a_whole_order_even_when_the_setting_says_100_percent()
    {
        _settings.PurchasePoints = _settings.PurchasePoints with { RedeemMaxPercent = 100 };

        var quote = await (await ServiceAsync(100000)).QuoteAsync(100000, 20m, default);

        Assert.True(quote.DiscountUsd < 20m);
        Assert.Equal(19.8m, quote.DiscountUsd);
    }

    [Fact]
    public async Task Cannot_spend_more_than_the_available_balance_and_is_inert_when_the_shop_is_off()
    {
        var service = await ServiceAsync(200);
        Assert.Equal(2m, (await service.QuoteAsync(10000, 100m, default)).DiscountUsd);

        _settings.Features = new FeatureCenterSettingsPayload();
        Assert.Equal(0, (await service.QuoteAsync(200, 100m, default)).Stars);
    }

    [Fact]
    public async Task Respects_the_minimum_redemption()
    {
        _settings.PurchasePoints = _settings.PurchasePoints with { MinRedeemStars = 300 };

        var quote = await (await ServiceAsync(200)).QuoteAsync(200, 100m, default);

        Assert.Equal(0, quote.Stars);
        Assert.Equal("BelowMinimum", quote.Problem);
    }
}
