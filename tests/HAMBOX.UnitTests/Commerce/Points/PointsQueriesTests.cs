using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Features.Account.Points.GetPointsHistory;
using HAMBOX.Modules.Commerce.Application.Features.Account.Points.GetPointsSummary;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Points;

/// <summary>The customer-facing read side: balance, total earned, history, pagination and isolation between customers.</summary>
public sealed class PointsQueriesTests
{
    private readonly CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;
    private readonly FakeCommerceSettingsProvider _settings = new()
    {
        PurchasePoints = new PurchasePointsSettingsPayload(true, 1m, PurchasePointsAmountBasis.NetOfTax),
    };

    private PurchasePointsService Points => new(_db, _settings, NullLogger<PurchasePointsService>.Instance);

    private static Order PaidCompleted(string userId, decimal subtotal)
    {
        var order = Order.Create(
            userId, $"ORD-{Guid.NewGuid():N}", "buyer@example.com", "US", "Development",
            subtotal, 0m, 0m, subtotal, []);
        order.RecordPayment("Development", Guid.NewGuid().ToString());
        order.Complete();
        return order;
    }

    private Task<Result<PointsSummaryDto>> SummaryFor(string? userId) =>
        new GetPointsSummaryQueryHandler(new FakeCurrentUserService(userId), _settings, new HAMBOX.Modules.Commerce.Application.Points.StarsLedgerService(_db))
            .Handle(new GetPointsSummaryQuery(), CancellationToken.None);

    private Task<Result<PagedResult<PointsTransactionDto>>> HistoryFor(
        string? userId, int page = 1, int size = 10, string? type = null) =>
        new GetPointsHistoryQueryHandler(_db, new FakeCurrentUserService(userId))
            .Handle(new GetPointsHistoryQuery(page, size, type), CancellationToken.None);

    [Fact]
    public async Task Summary_BalanceAndTotalEarned_ReflectEarnedAndReversedRows()
    {
        var first = PaidCompleted("alice", 100m);
        await Points.AwardForOrderAsync(first, CancellationToken.None);
        await Points.AwardForOrderAsync(PaidCompleted("alice", 40m), CancellationToken.None);
        await Points.ReverseForOrderAsync(first, 25m, CancellationToken.None);

        var summary = (await SummaryFor("alice")).Value;

        Assert.Equal(140, summary.TotalEarned);
        Assert.Equal(115, summary.Balance);
        Assert.True(summary.Enabled);
        Assert.Equal(1m, summary.PointsPerUsd);
    }

    [Fact]
    public async Task Summary_NewCustomer_IsZero_AndReportsTheProgramState()
    {
        _settings.PurchasePoints = new PurchasePointsSettingsPayload(false, 3m, PurchasePointsAmountBasis.Total);

        var summary = (await SummaryFor("nobody")).Value;

        Assert.Equal(0, summary.Balance);
        Assert.Equal(0, summary.TotalEarned);
        Assert.False(summary.Enabled);
        Assert.Equal(3m, summary.PointsPerUsd);
    }

    [Fact]
    public async Task Summary_AndHistory_NeverExposeAnotherCustomersPoints()
    {
        await Points.AwardForOrderAsync(PaidCompleted("alice", 30m), CancellationToken.None);
        await Points.AwardForOrderAsync(PaidCompleted("bob", 900m), CancellationToken.None);

        Assert.Equal(30, (await SummaryFor("alice")).Value.Balance);
        Assert.Equal(900, (await SummaryFor("bob")).Value.Balance);

        var aliceHistory = (await HistoryFor("alice")).Value;
        var row = Assert.Single(aliceHistory.Items);
        Assert.Equal(30, row.Points);
        Assert.Equal(1, aliceHistory.TotalCount);
    }

    [Fact]
    public async Task Queries_RequireAnAuthenticatedCustomer()
    {
        await Points.AwardForOrderAsync(PaidCompleted("alice", 30m), CancellationToken.None);

        Assert.True((await SummaryFor(null)).IsFailure);
        Assert.True((await HistoryFor(null)).IsFailure);
    }

    [Fact]
    public async Task History_ShowsTypeOrderReferenceReasonAndDate_NewestFirst()
    {
        var order = PaidCompleted("alice", 50m);
        await Points.AwardForOrderAsync(order, CancellationToken.None);
        await Task.Delay(15);
        await Points.ReverseForOrderAsync(order, refundedAmountUsd: null, CancellationToken.None);

        var items = (await HistoryFor("alice")).Value.Items.ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("Reversed", items[0].Type);
        Assert.Equal(-50, items[0].Points);
        Assert.Equal("Earned", items[1].Type);
        Assert.Equal(50, items[1].Points);
        Assert.All(items, i =>
        {
            Assert.Equal(order.Id, i.OrderId);
            Assert.Equal(order.OrderNumber, i.OrderNumber);
            Assert.False(string.IsNullOrWhiteSpace(i.Description));
        });
        Assert.True(items[0].OccurredOnUtc >= items[1].OccurredOnUtc);
    }

    [Fact]
    public async Task History_PaginatesAndFiltersByType()
    {
        for (var i = 0; i < 25; i++)
        {
            await Points.AwardForOrderAsync(PaidCompleted("alice", 10m), CancellationToken.None);
        }

        var lastOrder = PaidCompleted("alice", 10m);
        await Points.AwardForOrderAsync(lastOrder, CancellationToken.None);
        await Points.ReverseForOrderAsync(lastOrder, refundedAmountUsd: null, CancellationToken.None);

        var page1 = (await HistoryFor("alice", page: 1, size: 10)).Value;
        var page3 = (await HistoryFor("alice", page: 3, size: 10)).Value;
        var reversedOnly = (await HistoryFor("alice", type: "Reversed")).Value;

        Assert.Equal(27, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(7, page3.Items.Count);
        Assert.Equal(1, reversedOnly.TotalCount);
        Assert.All(reversedOnly.Items, i => Assert.Equal("Reversed", i.Type));
    }

    [Fact]
    public async Task History_ClampsAbsurdPageSizes()
    {
        await Points.AwardForOrderAsync(PaidCompleted("alice", 10m), CancellationToken.None);

        var result = (await HistoryFor("alice", page: 0, size: 10_000)).Value;

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(100, result.PageSize);
    }
}
