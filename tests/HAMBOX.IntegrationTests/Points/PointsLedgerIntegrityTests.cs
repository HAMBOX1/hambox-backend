using HAMBOX.Application.PlatformSettings;
using HAMBOX.IntegrationTests.Referrals;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.IntegrationTests.Points;

/// <summary>
/// The ledger's duplicate protection against a real database. EF's InMemory provider ignores unique indexes, so
/// these run on file-based SQLite (like the referral tests) with two genuinely separate connections.
/// </summary>
public sealed class PointsLedgerIntegrityTests : IDisposable
{
    private readonly List<string> _tempDatabases = [];

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
                // best effort — a lingering handle on a temp file is not worth failing the run over
            }
        }
    }

    private string NewDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hambox-points-test-{Guid.NewGuid():N}.db");
        _tempDatabases.Add(path);
        return path;
    }

    private static CommerceDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=10")
            .Options;

        var db = new CommerceDbContext(options, new FakeCodeProtector());
        db.Database.EnsureCreated();
        return db;
    }

    private static PurchasePointsService ServiceFor(CommerceDbContext db) =>
        new(
            db,
            new FakeReferralPlatformSettingsProvider
            {
                PurchasePoints = new PurchasePointsSettingsPayload(true, 1m, PurchasePointsAmountBasis.NetOfTax),
            },
            NullLogger<PurchasePointsService>.Instance);

    private static Order PaidCompleted(decimal subtotal)
    {
        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "buyer@example.com", "US", "Development",
            subtotal, 0m, 0m, subtotal, []);
        order.RecordPayment("Development", Guid.NewGuid().ToString());
        order.Complete();
        return order;
    }

    [Fact]
    public async Task Database_RejectsASecondEarnRowForTheSameOrder()
    {
        var path = NewDatabasePath();
        await using var db = Open(path);
        var orderId = Guid.NewGuid();

        db.PointsTransactions.Add(PointsTransaction.Earn("user-1", orderId, "ORD-1", 10, 10m, 1m));
        await db.SaveChangesAsync();

        await using var second = Open(path);
        second.PointsTransactions.Add(PointsTransaction.Earn("user-1", orderId, "ORD-1", 10, 10m, 1m));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Equal(1, await db.PointsTransactions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentCompletionsOfTheSameOrder_CreditExactlyOnce()
    {
        var path = NewDatabasePath();
        await using var seed = Open(path);
        var order = PaidCompleted(75m);

        // Two workers (a webhook and a retry job, say) finish the same order at the same moment.
        var contexts = Enumerable.Range(0, 4).Select(_ => Open(path)).ToList();
        try
        {
            await Task.WhenAll(contexts.Select(c => ServiceFor(c).AwardForOrderAsync(order, CancellationToken.None)));
        }
        finally
        {
            foreach (var c in contexts)
            {
                await c.DisposeAsync();
            }
        }

        var rows = await seed.PointsTransactions.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(75, row.Points);
    }

    [Fact]
    public async Task ConcurrentReversalsOfTheSameRefund_TakeBackExactlyOnce()
    {
        var path = NewDatabasePath();
        await using var seed = Open(path);
        var order = PaidCompleted(80m);
        await ServiceFor(seed).AwardForOrderAsync(order, CancellationToken.None);

        var contexts = Enumerable.Range(0, 4).Select(_ => Open(path)).ToList();
        try
        {
            await Task.WhenAll(contexts.Select(c => ServiceFor(c).ReverseForOrderAsync(order, null, CancellationToken.None)));
        }
        finally
        {
            foreach (var c in contexts)
            {
                await c.DisposeAsync();
            }
        }

        var total = await seed.PointsTransactions.AsNoTracking().SumAsync(t => t.Points);
        Assert.Equal(0, total);
        Assert.Equal(2, await seed.PointsTransactions.CountAsync());
    }
}
