using HAMBOX.Application.Membership;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Features.Cosmetics;
using HAMBOX.Modules.Commerce.Application.Loyalty;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Cosmetics;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class CosmeticServiceTests
{
    private sealed class Access(bool elite) : IMembershipAccessProvider
    {
        public Task<MembershipAccessInfo> GetAccessInfoAsync(string? userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(elite ? MembershipAccessInfo.None with { HasActiveMembership = true, SubscriptionId = Guid.NewGuid() } : MembershipAccessInfo.None);

        public Task<ProductAccessInfo> GetProductAccessAsync(string? userId, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductAccessInfo.Unrestricted);

        public Task<IReadOnlyDictionary<Guid, ProductAccessInfo>> GetProductsAccessAsync(
            string? userId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ProductAccessInfo>>(new Dictionary<Guid, ProductAccessInfo>());
    }

    private const string User = "u1";

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;
    private readonly FakeCommerceSettingsProvider _settings = new() { Features = new FeatureCenterSettingsPayload(ProfileStyle: true) };

    private CosmeticService Service(bool elite = false) => new(
        _db, _settings, new LoyaltyRankService(_db, _settings, NullLogger<LoyaltyRankService>.Instance), new Access(elite), new StarsLedgerService(_db));

    private async Task<ProfileCosmetic> AddAsync(CosmeticUnlock unlock, int? price = null)
    {
        var item = ProfileCosmetic.Create(CosmeticKind.Frame, $"frame-{Guid.NewGuid():N}"[..14], "Frame", "إطار", null, null, unlock, null, null, price, true, 0);
        _db.ProfileCosmetics.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task Elite_items_are_wearable_only_while_the_subscription_is_active()
    {
        var item = await AddAsync(CosmeticUnlock.Elite);

        Assert.NotNull(await Service(elite: false).EquipAsync(User, item.Id, null, null, default));
        Assert.Null(await Service(elite: true).EquipAsync(User, item.Id, null, null, default));
        Assert.True((await Service(elite: true).GetAsync(User, default)).Items.Single().Equipped);
        Assert.False((await Service(elite: false).GetAsync(User, default)).Items.Single().Equipped);
    }

    [Fact]
    public async Task A_Stars_purchase_is_kept_and_cannot_be_bought_without_the_balance()
    {
        var item = await AddAsync(CosmeticUnlock.Stars, 200);

        Assert.Equal(CosmeticErrors.InsufficientStars, await Service().BuyAsync(User, item.Id, default));

        _db.PointsTransactions.Add(PointsTransaction.Credit(User, PointsTransactionType.Bonus, 250, "seed", "seed"));
        await _db.SaveChangesAsync();
        Assert.Null(await Service().BuyAsync(User, item.Id, default));
        Assert.Null(await Service().BuyAsync(User, item.Id, default));

        Assert.Equal(50, (await new StarsLedgerService(_db).GetBalanceAsync(User, default)).Available);
        Assert.Null(await Service().EquipAsync(User, item.Id, null, null, default));
    }

    [Fact]
    public async Task Disabled_feature_is_inert()
    {
        _settings.Features = new FeatureCenterSettingsPayload();
        await AddAsync(CosmeticUnlock.Free);

        Assert.Empty((await Service().GetAsync(User, default)).Items);
        Assert.Equal(CosmeticErrors.Disabled, await Service().BuyAsync(User, Guid.NewGuid(), default));
    }
}
