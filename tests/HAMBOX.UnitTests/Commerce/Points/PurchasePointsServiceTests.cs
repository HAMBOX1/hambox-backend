using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Points;

/// <summary>
/// Purchase points: calculation, eligibility, idempotency, reversal and the "completion seam" in
/// <see cref="ReferralLifecycleService"/> that every order-completion path already goes through.
/// </summary>
public sealed class PurchasePointsServiceTests
{
    private sealed class Harness
    {
        public Harness(bool enabled = true, decimal rate = 1m, string basis = PurchasePointsAmountBasis.NetOfTax)
        {
            Db = CommerceTestDbContextFactory.Create().Commerce;
            Settings = new FakeCommerceSettingsProvider
            {
                PurchasePoints = new PurchasePointsSettingsPayload(enabled, rate, basis),
            };
            Points = new PurchasePointsService(Db, Settings, NullLogger<PurchasePointsService>.Instance);
            Lifecycle = new ReferralLifecycleService(
                Db,
                Settings,
                new ReferralRewardService(Db, new FakeMembershipEngine()),
                Points,
                new FakeCommunicationService(),
                NullLogger<ReferralLifecycleService>.Instance);
        }

        public CommerceDbContext Db { get; }

        public FakeCommerceSettingsProvider Settings { get; }

        public PurchasePointsService Points { get; }

        public ReferralLifecycleService Lifecycle { get; }

        public void SetProgram(bool enabled, decimal rate, string basis = PurchasePointsAmountBasis.NetOfTax) =>
            Settings.PurchasePoints = new PurchasePointsSettingsPayload(enabled, rate, basis);

        public Task<List<PointsTransaction>> LedgerAsync(string? userId = null) =>
            Db.PointsTransactions.AsNoTracking()
                .Where(t => userId == null || t.UserId == userId)
                .OrderBy(t => t.Points < 0).ThenBy(t => t.Id)
                .ToListAsync();

        public async Task<int> BalanceAsync(string userId) =>
            await Db.PointsTransactions.Where(t => t.UserId == userId).SumAsync(t => (int?)t.Points) ?? 0;
    }

    private static Order PaidCompleted(
        string userId = "user-1", decimal subtotal = 100m, decimal discount = 0m, decimal tax = 0m, string paymentMethod = "Development")
    {
        var order = Order.Create(
            userId, $"ORD-{Guid.NewGuid():N}", "buyer@example.com", "US", paymentMethod,
            subtotal, discount, tax, subtotal - discount + tax, []);
        order.RecordPayment("Development", Guid.NewGuid().ToString());
        order.Complete();
        return order;
    }

    // ---- Calculation ------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 0, 0, 1, 10)]
    [InlineData(100, 0, 0, 2.5, 250)]
    [InlineData(19.99, 0, 0, 0.5, 9)] // floors — never rounds in the customer's favour
    [InlineData(1234.56, 0, 0, 1, 1234)]
    [InlineData(49.99, 10, 2, 1, 39)] // eligible = subtotal - discount; tax is excluded
    [InlineData(100, 100, 0, 1, 0)] // fully discounted order earns nothing
    [InlineData(0.99, 0, 0, 1, 0)] // under one point's worth
    public async Task Award_NetOfTaxBasis_FloorsEligibleAmountTimesRate(
        double subtotal, double discount, double tax, double rate, int expectedPoints)
    {
        var h = new Harness(rate: (decimal)rate);
        var order = PaidCompleted(subtotal: (decimal)subtotal, discount: (decimal)discount, tax: (decimal)tax);

        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        Assert.Equal(expectedPoints, await h.BalanceAsync("user-1"));
        Assert.Equal(expectedPoints > 0 ? 1 : 0, (await h.LedgerAsync()).Count);
    }

    [Fact]
    public async Task Award_TotalBasis_IncludesTaxAndGatewayFee()
    {
        var h = new Harness(basis: PurchasePointsAmountBasis.Total);

        await h.Points.AwardForOrderAsync(PaidCompleted(subtotal: 50m, discount: 10m, tax: 2m), CancellationToken.None);

        Assert.Equal(42, await h.BalanceAsync("user-1"));
    }

    [Fact]
    public async Task Award_RecordsTheAmountAndRateUsed()
    {
        var h = new Harness(rate: 2m);
        var order = PaidCompleted(subtotal: 30m);

        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        var row = Assert.Single(await h.LedgerAsync());
        Assert.Equal(PointsTransactionType.Earned, row.Type);
        Assert.Equal(60, row.Points);
        Assert.Equal(30m, row.EligibleAmountUsd);
        Assert.Equal(2m, row.PointsPerUsd);
        Assert.Equal(order.Id, row.OrderId);
        Assert.Equal(order.OrderNumber, row.OrderNumber);
        Assert.Contains(order.OrderNumber, row.Description);
    }

    [Fact]
    public async Task Award_ProgramDisabledOrRateZero_AwardsNothing()
    {
        var h = new Harness(enabled: false);
        await h.Points.AwardForOrderAsync(PaidCompleted(), CancellationToken.None);

        h.SetProgram(enabled: true, rate: 0m);
        await h.Points.AwardForOrderAsync(PaidCompleted(), CancellationToken.None);

        Assert.Empty(await h.LedgerAsync());
    }

    // ---- Currency ---------------------------------------------------------------------------

    [Fact]
    public async Task Award_UsesStoredUsdAmount_EvenWhenTheGatewaySettlesInAnotherCurrency()
    {
        // Prices are stored in USD and conversion is display-only, so a DOT-Fawry (EGP) payment earns on the USD order amount.
        var h = new Harness();

        await h.Points.AwardForOrderAsync(PaidCompleted(subtotal: 20m, paymentMethod: "DotFawry"), CancellationToken.None);

        Assert.Equal(20, await h.BalanceAsync("user-1"));
    }

    [Fact]
    public async Task Award_MembershipOrder_EarnsOnItsPrice()
    {
        var h = new Harness();
        var order = Order.CreateForMembership(
            "user-1", "ORD-M1", "buyer@example.com", "US", "Development", 29.99m,
            Guid.NewGuid(), "Gold", MembershipCheckoutAction.Subscribe);
        order.RecordPayment("Development", "txn-1");
        order.Complete();

        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        Assert.Equal(29, await h.BalanceAsync("user-1"));
    }

    // ---- Eligibility ------------------------------------------------------------------------

    [Fact]
    public async Task Award_UnpaidPendingFailedCancelledAndRefundedOrders_EarnNothing()
    {
        var h = new Harness();

        Order Unpaid() => Order.Create("user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "Development", 100m, 0m, 0m, 100m, []);

        var pending = Unpaid();

        var paidButNotCompleted = Unpaid();
        paidButNotCompleted.RecordPayment("Development", "txn-p");

        var failed = Unpaid();
        failed.MarkFailed();

        var cancelled = Unpaid();
        cancelled.Cancel();

        var refunded = PaidCompleted();
        refunded.Refund();

        foreach (var order in new[] { pending, paidButNotCompleted, failed, cancelled, refunded })
        {
            await h.Points.AwardForOrderAsync(order, CancellationToken.None);
        }

        Assert.Empty(await h.LedgerAsync());
    }

    // ---- Idempotency ------------------------------------------------------------------------

    [Fact]
    public async Task Award_RepeatedForTheSameOrder_CreditsOnce()
    {
        var h = new Harness();
        var order = PaidCompleted(subtotal: 40m);

        await h.Points.AwardForOrderAsync(order, CancellationToken.None);
        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        Assert.Single(await h.LedgerAsync());
        Assert.Equal(40, await h.BalanceAsync("user-1"));
    }

    [Fact]
    public async Task CompletionSeam_RetriedWebhookOrJob_CreditsOnce()
    {
        var h = new Harness();
        var order = PaidCompleted(subtotal: 25m);

        // Payment webhooks, retry jobs and the admin "complete" action all land on ProcessOrderCompletedAsync.
        await h.Lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);
        await h.Lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);
        await h.Lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        Assert.Equal(25, await h.BalanceAsync("user-1"));
        Assert.Single(await h.LedgerAsync());
    }

    [Fact]
    public async Task CompletionSeam_ApprovingAHeldReferral_NeverAwardsPointsForTheOldOrder()
    {
        var h = new Harness(enabled: true);
        h.Settings.Referral = new ReferralSettingsPayload(Enabled: true, PointsPerReferral: 0, PointValueUsd: 0m, RewardExpiryDays: 30);

        var referrer = "referrer-1";
        var referred = "referred-1";
        h.Db.ReferralProfiles.Add(ReferralProfile.CreateForUser(referrer));
        var entry = ReferralHistoryEntry.CreatePending(referrer, referred, "friend@example.com", DateTime.UtcNow.AddDays(30));
        entry.FlagForReview("Looks like farming.");
        h.Db.ReferralHistoryEntries.Add(entry);
        h.Db.Orders.Add(PaidCompleted(userId: referred, subtotal: 80m));
        await h.Db.SaveChangesAsync();

        var result = await h.Lifecycle.AdminApproveReviewAsync(entry.Id, "admin-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReferralStatus.Qualified, (await h.Db.ReferralHistoryEntries.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await h.LedgerAsync());
    }

    // ---- Reversal ---------------------------------------------------------------------------

    [Fact]
    public async Task Reverse_CancelledOrFullyRefunded_TakesBackEverythingOnce()
    {
        var h = new Harness();
        var order = PaidCompleted(subtotal: 100m);
        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        await h.Points.ReverseForOrderAsync(order, refundedAmountUsd: null, CancellationToken.None);
        await h.Points.ReverseForOrderAsync(order, refundedAmountUsd: null, CancellationToken.None);

        var rows = await h.LedgerAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Type == PointsTransactionType.Earned && r.Points == 100);
        Assert.Contains(rows, r => r.Type == PointsTransactionType.Reversed && r.Points == -100);
        Assert.Equal(0, await h.BalanceAsync("user-1"));
    }

    [Fact]
    public async Task Reverse_OrderThatNeverEarned_IsANoOp()
    {
        var h = new Harness(enabled: false);

        await h.Points.ReverseForOrderAsync(PaidCompleted(), refundedAmountUsd: null, CancellationToken.None);

        Assert.Empty(await h.LedgerAsync());
    }

    [Fact]
    public async Task Reverse_PartialRefunds_AreCumulativeAndNeverExceedWhatWasEarned()
    {
        var h = new Harness();
        var order = PaidCompleted(subtotal: 100m); // earns 100
        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        await h.Points.ReverseForOrderAsync(order, 25m, CancellationToken.None);
        Assert.Equal(75, await h.BalanceAsync("user-1"));

        // The same refund reported again (a retried callback) changes nothing.
        await h.Points.ReverseForOrderAsync(order, 25m, CancellationToken.None);
        Assert.Equal(75, await h.BalanceAsync("user-1"));

        // A later, larger cumulative refund only takes back the difference.
        await h.Points.ReverseForOrderAsync(order, 60m, CancellationToken.None);
        Assert.Equal(40, await h.BalanceAsync("user-1"));

        // Refunding the whole total (or more) reverses whatever is left, and no more.
        await h.Points.ReverseForOrderAsync(order, 100m, CancellationToken.None);
        await h.Points.ReverseForOrderAsync(order, 500m, CancellationToken.None);
        Assert.Equal(0, await h.BalanceAsync("user-1"));
        Assert.Equal(0, (await h.LedgerAsync()).Sum(r => r.Points));
    }

    [Fact]
    public async Task Reverse_PartialRefund_RoundsToTheNearestPoint()
    {
        var h = new Harness(rate: 0.33m);
        var order = PaidCompleted(subtotal: 100m); // earns 33
        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        await h.Points.ReverseForOrderAsync(order, 50m, CancellationToken.None); // 33 * 50% = 16.5 -> 17

        Assert.Equal(33 - 17, await h.BalanceAsync("user-1"));
    }

    [Fact]
    public async Task CompletionSeam_RefundGivesPointsBack()
    {
        var h = new Harness();
        var order = PaidCompleted(subtotal: 60m);
        await h.Lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        await h.Lifecycle.ReverseForOrderAsync(order, CancellationToken.None);

        Assert.Equal(0, await h.BalanceAsync("user-1"));
        Assert.Equal(2, (await h.LedgerAsync()).Count);
    }

    // ---- Settings changes -------------------------------------------------------------------

    [Fact]
    public async Task ChangingTheRate_AffectsOnlyFuturePurchases_AndReversalsUseTheOriginalAward()
    {
        var h = new Harness(rate: 1m);
        var first = PaidCompleted(subtotal: 50m);
        await h.Points.AwardForOrderAsync(first, CancellationToken.None);

        h.SetProgram(enabled: true, rate: 10m);
        var second = PaidCompleted(subtotal: 50m);
        await h.Points.AwardForOrderAsync(second, CancellationToken.None);

        var firstRow = (await h.LedgerAsync()).Single(r => r.OrderId == first.Id);
        Assert.Equal(50, firstRow.Points);
        Assert.Equal(1m, firstRow.PointsPerUsd);
        Assert.Equal(500, (await h.LedgerAsync()).Single(r => r.OrderId == second.Id).Points);

        // Refunding the first order takes back the 50 it earned — not 500 at today's rate.
        await h.Points.ReverseForOrderAsync(first, refundedAmountUsd: null, CancellationToken.None);

        Assert.Equal(500, await h.BalanceAsync("user-1"));
    }

    [Fact]
    public async Task DisablingTheProgram_StillLetsExistingPointsBeReversed()
    {
        var h = new Harness();
        var order = PaidCompleted(subtotal: 70m);
        await h.Points.AwardForOrderAsync(order, CancellationToken.None);

        h.SetProgram(enabled: false, rate: 1m);
        await h.Points.ReverseForOrderAsync(order, refundedAmountUsd: null, CancellationToken.None);

        Assert.Equal(0, await h.BalanceAsync("user-1"));
    }

    // ---- Customer isolation -----------------------------------------------------------------

    [Fact]
    public async Task Points_AreAttributedToTheBuyerOnly()
    {
        var h = new Harness();

        await h.Points.AwardForOrderAsync(PaidCompleted(userId: "alice", subtotal: 30m), CancellationToken.None);
        await h.Points.AwardForOrderAsync(PaidCompleted(userId: "bob", subtotal: 70m), CancellationToken.None);

        Assert.Equal(30, await h.BalanceAsync("alice"));
        Assert.Equal(70, await h.BalanceAsync("bob"));
    }
}
