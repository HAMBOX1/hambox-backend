using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Features.Missions;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Missions;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class MissionServiceTests
{
    private const string User = "u1";

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;
    private readonly FakeCommerceSettingsProvider _settings = new() { Features = new FeatureCenterSettingsPayload(Missions: true) };

    private MissionService Service => new(_db, _settings, new StarsLedgerService(_db), NullLogger<MissionService>.Instance);

    private async Task CompleteOrderAsync(decimal amount, bool deliver = true)
    {
        var order = Order.Create(User, $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "Development", amount, 0m, 0m, amount, []);
        order.RecordPayment("Development", Guid.NewGuid().ToString());
        if (deliver)
        {
            order.Complete();
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
    }

    private async Task AddMissionAsync(MissionKind kind, decimal target, int reward) =>
        await AddAsync(Mission.Create("Five orders", "خمسة طلبات", null, null, kind, target, reward, "founder", true, null, null, 0));

    private async Task AddAsync(Mission mission)
    {
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Reaching_the_target_pays_the_reward_once_only()
    {
        await AddMissionAsync(MissionKind.CompletedOrders, 2, 100);
        await CompleteOrderAsync(10m);
        await Service.EvaluateAsync(User, default);
        Assert.Empty(_db.MissionCompletions);

        await CompleteOrderAsync(10m);
        await Service.EvaluateAsync(User, default);
        await Service.EvaluateAsync(User, default);

        Assert.Single(_db.MissionCompletions);
        Assert.Equal(100, (await new StarsLedgerService(_db).GetBalanceAsync(User, default)).Available);
    }

    [Fact]
    public async Task An_order_that_is_not_delivered_never_counts()
    {
        await AddMissionAsync(MissionKind.CompletedOrders, 1, 50);
        await CompleteOrderAsync(10m, deliver: false);

        await Service.EvaluateAsync(User, default);

        Assert.Empty(_db.MissionCompletions);
    }

    [Fact]
    public async Task Spend_missions_use_delivered_spend_and_progress_is_capped_at_the_target()
    {
        await AddMissionAsync(MissionKind.SpendUsd, 100, 25);
        await CompleteOrderAsync(40m);

        var progress = (await Service.GetProgressAsync(User, default)).Single();
        Assert.Equal(40m, progress.Current);
        Assert.False(progress.Completed);

        await CompleteOrderAsync(300m);
        var done = (await Service.GetProgressAsync(User, default)).Single();
        Assert.True(done.Completed);
        Assert.Equal(100m, done.Current);
    }

    [Fact]
    public async Task Disabled_feature_is_inert()
    {
        _settings.Features = new FeatureCenterSettingsPayload();
        await AddMissionAsync(MissionKind.CompletedOrders, 1, 50);
        await CompleteOrderAsync(10m);

        await Service.EvaluateAsync(User, default);

        Assert.Empty(_db.MissionCompletions);
        Assert.Empty(await Service.GetProgressAsync(User, default));
    }
}
