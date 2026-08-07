using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.IntegrationTests.Referrals;

/// <summary>
/// Covers the referral points lifecycle end to end against a real <see cref="CommerceDbContext"/>
/// backed by file-based SQLite — the only provider available here that both translates
/// <c>ExecuteUpdateAsync</c> to real SQL (unlike EF Core's InMemory provider, which doesn't support it
/// at all) and lets two genuinely separate connections race against the same database (unlike a single
/// shared in-memory connection). Only the external dependencies (Platform Settings, Communication,
/// Membership) are faked.
/// </summary>
public sealed class ReferralLifecycleServiceTests : IDisposable
{
    private readonly List<string> _tempDatabases = [];

    private string CreateDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hambox-referral-test-{Guid.NewGuid():N}.db");
        _tempDatabases.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempDatabases)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // best effort — a lingering handle on a temp file is not worth failing the test run over
            }
        }
    }

    private static CommerceDbContext CreateDbContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseSqlite($"Data Source={dbPath};Default Timeout=5")
            .Options;

        var db = new CommerceDbContext(options, new FakeCodeProtector());
        db.Database.EnsureCreated();
        return db;
    }

    private static (CommerceDbContext Db, ReferralLifecycleService Lifecycle, FakeCommunicationService Communication) CreateHarness(
        string dbPath, FakeReferralPlatformSettingsProvider? settings = null)
    {
        var db = CreateDbContext(dbPath);
        settings ??= new FakeReferralPlatformSettingsProvider();
        var communication = new FakeCommunicationService();
        var reward = new ReferralRewardService(db, new FakeMembershipEngine());
        var lifecycle = new ReferralLifecycleService(db, settings, reward, communication, NullLogger<ReferralLifecycleService>.Instance);

        return (db, lifecycle, communication);
    }

    private static Order CreateCompletedOrder(string userId, decimal totalAmount)
    {
        var order = Order.Create(
            userId,
            $"ORD-{Guid.NewGuid():N}",
            "buyer@example.com",
            "US",
            "Development",
            totalAmount,
            0m,
            0m,
            totalAmount,
            []);

        order.RecordPayment("Development", Guid.NewGuid().ToString());
        order.Complete();
        return order;
    }

    private static async Task SeedPendingReferralAsync(
        CommerceDbContext db, string referrerUserId, string referredUserId, DateTime? expiresOnUtc = null)
    {
        db.ReferralProfiles.Add(ReferralProfile.CreateForUser(referrerUserId));
        db.ReferralHistoryEntries.Add(
            ReferralHistoryEntry.CreatePending(
                referrerUserId, referredUserId, "friend@example.com", expiresOnUtc ?? DateTime.UtcNow.AddDays(30)));
        await db.SaveChangesAsync();
    }

    // ---- Reward issuance -------------------------------------------------------------------

    [Fact]
    public async Task ProcessOrderCompletedAsync_FirstCompletedOrder_QualifiesAndRewardsReferrer()
    {
        var dbPath = CreateDatabasePath();
        var (db, lifecycle, communication) = CreateHarness(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();
        await SeedPendingReferralAsync(db, referrerId, referredId);

        var order = CreateCompletedOrder(referredId, 100m);

        var outcome = await lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        Assert.Equal(ReferralQualificationOutcome.Qualified, outcome);

        var entry = await db.ReferralHistoryEntries.AsNoTracking().SingleAsync(h => h.ReferredUserId == referredId);
        Assert.Equal(ReferralStatus.Rewarded, entry.Status);
        Assert.Equal(100, entry.PointsEarned); // the fake settings' default PointsPerReferral

        var profile = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(100, profile.LifetimePoints);
        Assert.Equal(1, profile.SuccessfulReferrals);

        Assert.Contains(communication.Sent, r => r.TemplateKey == "ReferralRewardEarned");

        var auditActions = await db.ReferralAuditLogs.AsNoTracking()
            .Where(a => a.ReferralHistoryEntryId == entry.Id)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Contains(ReferralAuditAction.Qualified, auditActions);
        Assert.Contains(ReferralAuditAction.Rewarded, auditActions);
    }

    [Fact]
    public async Task AwardPointsAsync_AppliesMembershipReferralMultiplier()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        db.ReferralProfiles.Add(ReferralProfile.CreateForUser(referrerId));
        await db.SaveChangesAsync();

        var membership = new FakeMembershipEngine { Snapshot = MembershipSnapshot.None with { ReferralMultiplier = 2m } };
        var reward = new ReferralRewardService(db, membership);

        var awarded = await reward.AwardPointsAsync(referrerId, 10, CancellationToken.None);

        Assert.Equal(20, awarded);

        var profile = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(20, profile.LifetimePoints);
    }

    [Fact]
    public async Task AwardPointsAsync_CrossingTierThreshold_UpdatesTierFromCentralizedPolicy()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        db.ReferralProfiles.Add(ReferralProfile.CreateForUser(referrerId));
        await db.SaveChangesAsync();

        var reward = new ReferralRewardService(db, new FakeMembershipEngine());
        await reward.AwardPointsAsync(referrerId, 100, CancellationToken.None);

        var profile = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(100, profile.LifetimePoints);
        Assert.Equal(ReferralTierPolicy.ResolveTier(100), profile.Tier);
        Assert.Equal("Bronze", profile.Tier);
    }

    // ---- Duplicate completion ---------------------------------------------------------------

    [Fact]
    public async Task ProcessOrderCompletedAsync_CalledTwiceForSameOrder_DoesNotDoubleReward()
    {
        var dbPath = CreateDatabasePath();
        var (db, lifecycle, _) = CreateHarness(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();
        await SeedPendingReferralAsync(db, referrerId, referredId);

        var order = CreateCompletedOrder(referredId, 100m);

        var first = await lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);
        var second = await lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        Assert.Equal(ReferralQualificationOutcome.Qualified, first);
        Assert.Equal(ReferralQualificationOutcome.NotApplicable, second);

        var profile = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(100, profile.LifetimePoints);
        Assert.Equal(1, profile.SuccessfulReferrals);
    }

    // ---- Concurrent checkout -----------------------------------------------------------------

    [Fact]
    public async Task ProcessOrderCompletedAsync_TwoConcurrentOrdersForSameReferredUser_OnlyOneQualifies()
    {
        var dbPath = CreateDatabasePath();
        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();

        using (var seedDb = CreateDbContext(dbPath))
        {
            await SeedPendingReferralAsync(seedDb, referrerId, referredId);
        }

        // Two independent connections/DbContexts against the same on-disk database, simulating two
        // concurrent checkouts racing to complete the referred user's first order.
        var (dbA, lifecycleA, _) = CreateHarness(dbPath);
        var (dbB, lifecycleB, _) = CreateHarness(dbPath);

        var orderA = CreateCompletedOrder(referredId, 50m);
        var orderB = CreateCompletedOrder(referredId, 200m);

        var results = await Task.WhenAll(
            lifecycleA.ProcessOrderCompletedAsync(orderA, CancellationToken.None),
            lifecycleB.ProcessOrderCompletedAsync(orderB, CancellationToken.None));

        // Exactly one order wins. The loser's outcome depends on exactly how the race interleaves —
        // Ineligible if its read landed before the winner committed (then lost on save), NotApplicable
        // if its read landed after (the entry was already gone) — either is a correct "did not qualify".
        Assert.Single(results, r => r == ReferralQualificationOutcome.Qualified);
        Assert.Single(results, r => r is ReferralQualificationOutcome.Ineligible or ReferralQualificationOutcome.NotApplicable);

        using var assertDb = CreateDbContext(dbPath);
        var profile = await assertDb.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(1, profile.SuccessfulReferrals); // credited for exactly one referral, never zero or two

        var entry = await assertDb.ReferralHistoryEntries.AsNoTracking().SingleAsync(h => h.ReferredUserId == referredId);
        Assert.Equal(ReferralStatus.Rewarded, entry.Status);
    }

    // ---- Expiry --------------------------------------------------------------------------------

    [Fact]
    public async Task ProcessOrderCompletedAsync_ExpiredReferral_MarksExpiredAndAwardsNothing()
    {
        var dbPath = CreateDatabasePath();
        var (db, lifecycle, _) = CreateHarness(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();
        await SeedPendingReferralAsync(db, referrerId, referredId, expiresOnUtc: DateTime.UtcNow.AddDays(-1));

        var order = CreateCompletedOrder(referredId, 100m);

        var outcome = await lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        Assert.Equal(ReferralQualificationOutcome.Ineligible, outcome);

        var entry = await db.ReferralHistoryEntries.AsNoTracking().SingleAsync(h => h.ReferredUserId == referredId);
        Assert.Equal(ReferralStatus.Expired, entry.Status);
        Assert.Equal(0, entry.PointsEarned);

        var profile = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(0, profile.LifetimePoints);
    }

    // ---- Refund reversal --------------------------------------------------------------------

    [Fact]
    public async Task ReverseForOrderAsync_RewardedReferral_ClawsBackPointsAndMarksReversed()
    {
        var dbPath = CreateDatabasePath();
        var (db, lifecycle, _) = CreateHarness(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();
        await SeedPendingReferralAsync(db, referrerId, referredId);

        var order = CreateCompletedOrder(referredId, 200m);
        await lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        var beforeReversal = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.True(beforeReversal.LifetimePoints > 0);

        await lifecycle.ReverseForOrderAsync(order, CancellationToken.None);

        var entry = await db.ReferralHistoryEntries.AsNoTracking().SingleAsync(h => h.ReferredUserId == referredId);
        Assert.Equal(ReferralStatus.Reversed, entry.Status);

        var afterReversal = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(0, afterReversal.LifetimePoints);
        Assert.Equal(0, afterReversal.SuccessfulReferrals);

        var auditActions = await db.ReferralAuditLogs.AsNoTracking()
            .Where(a => a.ReferralHistoryEntryId == entry.Id)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Contains(ReferralAuditAction.Reversed, auditActions);
    }

    [Fact]
    public async Task ReverseForOrderAsync_CalledTwice_ReversesExactlyOnce()
    {
        var dbPath = CreateDatabasePath();
        var (db, lifecycle, _) = CreateHarness(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();
        await SeedPendingReferralAsync(db, referrerId, referredId);

        var order = CreateCompletedOrder(referredId, 200m);
        await lifecycle.ProcessOrderCompletedAsync(order, CancellationToken.None);

        await lifecycle.ReverseForOrderAsync(order, CancellationToken.None);
        await lifecycle.ReverseForOrderAsync(order, CancellationToken.None); // second refund attempt on the same order

        var profile = await db.ReferralProfiles.AsNoTracking().SingleAsync(p => p.UserId == referrerId);
        Assert.Equal(0, profile.LifetimePoints); // never driven negative by a double clawback
    }

    [Fact]
    public async Task ReverseForOrderAsync_StillPendingReferral_ExpiresRatherThanReverses()
    {
        var dbPath = CreateDatabasePath();
        var (db, lifecycle, _) = CreateHarness(dbPath);

        var referrerId = Guid.NewGuid().ToString();
        var referredId = Guid.NewGuid().ToString();
        await SeedPendingReferralAsync(db, referrerId, referredId);

        // Order cancelled/refunded before ever completing — ProcessOrderCompletedAsync never ran.
        var order = CreateCompletedOrder(referredId, 200m);
        await lifecycle.ReverseForOrderAsync(order, CancellationToken.None);

        var entry = await db.ReferralHistoryEntries.AsNoTracking().SingleAsync(h => h.ReferredUserId == referredId);
        Assert.Equal(ReferralStatus.Expired, entry.Status);
    }
}
