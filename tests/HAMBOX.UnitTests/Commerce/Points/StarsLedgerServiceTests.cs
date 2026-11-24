using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class StarsLedgerServiceTests
{
    private const string User = "u1";

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;

    private StarsLedgerService Ledger => new(_db);

    private async Task SeedAsync(params PointsTransaction[] rows)
    {
        _db.PointsTransactions.AddRange(rows);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public void Stars_permission_names_match_the_identity_constants()
    {
        Assert.Equal(
            HAMBOX.Modules.Identity.Application.Authorization.PermissionConstants.Stars.ApproveOverride,
            HAMBOX.Modules.Commerce.Application.Points.StarsPermissions.ApproveOverride);
    }

    [Fact]
    public async Task Pending_credit_is_not_spendable_until_its_hold_passes()
    {
        await SeedAsync(
            PointsTransaction.Credit(User, PointsTransactionType.Bonus, 100, "b", "k1"),
            PointsTransaction.Credit(User, PointsTransactionType.Bonus, 50, "held", "k2", StarsStatus.Pending, DateTimeOffset.UtcNow.AddDays(1)),
            PointsTransaction.Credit(User, PointsTransactionType.Bonus, 25, "due", "k3", StarsStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-1)));

        var balance = await Ledger.GetBalanceAsync(User, default);

        Assert.Equal(125, balance.Available);
        Assert.Equal(50, balance.Pending);
    }

    [Fact]
    public async Task Reserved_debit_reduces_available_and_cancelled_rows_count_for_nothing()
    {
        var reserve = PointsTransaction.Debit(User, PointsTransactionType.Redeemed, 80, "r", "k2", StarsStatus.Reserved);
        var cancelled = PointsTransaction.Credit(User, PointsTransactionType.Bonus, 999, "x", "k3", StarsStatus.Pending);
        cancelled.Cancel();
        await SeedAsync(PointsTransaction.Credit(User, PointsTransactionType.Bonus, 100, "b", "k1"), reserve, cancelled);

        var balance = await Ledger.GetBalanceAsync(User, default);

        Assert.Equal(20, balance.Available);
        Assert.Equal(80, balance.Reserved);
        Assert.Equal(100, balance.LifetimeEarned);
    }

    [Fact]
    public async Task Credit_and_debit_are_idempotent_per_key()
    {
        Assert.True(await Ledger.CreditAsync(User, PointsTransactionType.ReferralReward, 50, "r", "referral:1"));
        Assert.False(await Ledger.CreditAsync(User, PointsTransactionType.ReferralReward, 50, "r", "referral:1"));
        Assert.True(await Ledger.DebitAsync(User, PointsTransactionType.Reversed, 50, "r", "referral-reverse:1"));
        Assert.False(await Ledger.DebitAsync(User, PointsTransactionType.Reversed, 50, "r", "referral-reverse:1"));

        Assert.Equal(0, (await Ledger.GetBalanceAsync(User, default)).Available);
    }

    [Fact]
    public async Task Negative_adjustment_cannot_overdraw_and_positive_needs_a_reason()
    {
        await SeedAsync(PointsTransaction.Credit(User, PointsTransactionType.Bonus, 30, "b", "k1"));

        Assert.False(await Ledger.AdjustAsync(User, -31, "oops", "staff", default));
        Assert.True(await Ledger.AdjustAsync(User, -30, "fix", "staff", default));
        await Assert.ThrowsAsync<ArgumentException>(() => Ledger.AdjustAsync(User, 5, " ", "staff", default));
    }

    [Fact]
    public async Task Expired_credit_lapses_once_and_only_for_what_is_unspent()
    {
        await SeedAsync(
            PointsTransaction.Credit(User, PointsTransactionType.Bonus, 100, "old", "k1", expiresOnUtc: DateTimeOffset.UtcNow.AddMinutes(-5)),
            PointsTransaction.Debit(User, PointsTransactionType.Redeemed, 70, "spent", "k2"));

        var first = await Ledger.GetBalanceAsync(User, default);
        var second = await Ledger.GetBalanceAsync(User, default);

        Assert.Equal(0, first.Available);
        Assert.Equal(0, second.Available);
        Assert.Single(_db.PointsTransactions.Where(t => t.Type == PointsTransactionType.Expired));
    }
}
