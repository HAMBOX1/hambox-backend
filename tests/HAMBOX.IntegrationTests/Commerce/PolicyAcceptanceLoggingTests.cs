using HAMBOX.Modules.Legal.Domain.Legal;
using HAMBOX.Modules.Legal.Infrastructure.Persistence;
using HAMBOX.Modules.Legal.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Commerce;

/// <summary>
/// Audit R26 runtime proof, on real SQL Server: accepting the published policies at checkout writes one row per
/// required section with user, order, policy version, time, IP and device — and a re-published policy makes the
/// customer's previous acceptance stale until they accept the new version.
/// </summary>
public sealed class PolicyAcceptanceLoggingTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HamboxPolicyAcceptance_{Guid.NewGuid():N}";
    private string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private LegalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<LegalDbContext>().UseSqlServer(ConnectionString).Options);

    private static LegalSection NewPublishedSection(string slug, bool requireAcceptance)
    {
        var section = LegalSection.Create(slug);
        section.UpdateMetadata(slug, null, null, 0, null, null, null, null, null, true, true, requireAcceptance);
        var draft = section.CreateDraftVersion(slug.ToUpperInvariant(), null, "content", null, null);
        section.PublishVersion(draft.Id, "admin-1");
        return section;
    }

    [Fact]
    public async Task CheckoutAcceptance_RecordsUserOrderVersionTimeIpAndDevice_ForEveryRequiredPolicy()
    {
        var orderId = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            db.LegalSections.Add(NewPublishedSection("terms", requireAcceptance: true));
            db.LegalSections.Add(NewPublishedSection("refund", requireAcceptance: true));
            db.LegalSections.Add(NewPublishedSection("about", requireAcceptance: false));
            await db.SaveChangesAsync();

            await LegalAcceptanceRecorder.RecordAsync(
                db, "user-42", "198.51.100.7", "Mozilla/5.0 (Test)", "ar", orderId);
            await db.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var rows = await verify.LegalSectionAcceptances.AsNoTracking().Where(a => a.OrderId == orderId).ToListAsync();

        Assert.Equal(2, rows.Count); // the two required policies, not the informational one
        Assert.All(rows, row =>
        {
            Assert.Equal("user-42", row.UserId);
            Assert.Equal(1, row.VersionNumber);
            Assert.Equal("198.51.100.7", row.IpAddress);
            Assert.Equal("Mozilla/5.0 (Test)", row.UserAgent);
            Assert.Equal("ar", row.Language);
            Assert.True(row.AcceptedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
        });
    }

    [Fact]
    public async Task RepublishedPolicy_MakesAnEarlierAcceptanceStale()
    {
        await using var db = CreateContext();
        var terms = NewPublishedSection("terms", requireAcceptance: true);
        db.LegalSections.Add(terms);
        await db.SaveChangesAsync();

        await LegalAcceptanceRecorder.RecordAsync(db, "user-7", "203.0.113.1", "UA", "en", Guid.NewGuid());
        await db.SaveChangesAsync();
        Assert.Empty(await LegalAcceptanceRecorder.GetStaleSlugsAsync(db, "user-7"));

        // The admin publishes version 2.
        var tracked = await db.LegalSections.Include(s => s.Versions).SingleAsync(s => s.Id == terms.Id);
        var v2 = tracked.CreateDraftVersion("TERMS", null, "new content", null, "v2");
        db.LegalSectionVersions.Add(v2); // client-assigned key: tell EF it is new
        tracked.PublishVersion(v2.Id, "admin-1");
        await db.SaveChangesAsync();

        Assert.Equal(["terms"], await LegalAcceptanceRecorder.GetStaleSlugsAsync(db, "user-7"));
    }
}
