using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Archive;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Archive;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Archive;

public sealed class LegacyArchiveServiceTests
{
    private sealed class FakeUsers(string? verifiedEmail) : IUserDirectory
    {
        public Task<IReadOnlySet<string>> GetExistingUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(userIds.ToHashSet());

        public Task<IReadOnlyDictionary<string, string>> FindUserIdsByEmailAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<DateTimeOffset?> GetRegisteredOnAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);

        public Task<string?> GetEmailAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(verifiedEmail);

        public Task<string?> GetVerifiedEmailAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(verifiedEmail);

        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    private readonly Modules.Commerce.Infrastructure.Persistence.CommerceDbContext _db = CommerceTestDbContextFactory.Create().Commerce;

    private LegacyArchiveService Service(string? verifiedEmail = null) => new(_db, new FakeUsers(verifiedEmail), new StarsLedgerService(_db));

    private static ParsedArchive Archive() => new(
        [
            new ArchiveCustomerRow(2, "C-1", "Old.Customer@Example.com", "+201000000000", "Old Customer", null, 250),
            new ArchiveCustomerRow(3, "C-2", "other@example.com", null, "Other", null, 0),
            new ArchiveCustomerRow(4, "C-1", "dup@example.com", null, "Dup", null, 0),
            new ArchiveCustomerRow(5, null, "x@example.com", null, "No id", null, 0),
        ],
        [
            new ArchiveOrderRow(2, "O-1", "C-1", 19.99m, "USD", "Completed", null),
            new ArchiveOrderRow(3, "O-2", "C-404", 5m, "USD", "Completed", null),
        ]);

    [Fact]
    public async Task Preview_writes_nothing_but_reports_what_would_happen()
    {
        var report = await Service().ImportAsync(Archive(), commit: false, default);

        Assert.False(report.Committed);
        Assert.Equal(2, report.NewCustomers);
        Assert.Equal(1, report.NewOrders);
        Assert.Equal(250, report.TotalPromisedStars);
        Assert.Contains(report.Issues, i => i.Reason.Contains("Duplicate customer id"));
        Assert.Contains(report.Issues, i => i.Reason.Contains("Missing customer id"));
        Assert.Contains(report.Issues, i => i.Reason.Contains("not in the archive"));
        Assert.Empty(_db.LegacyCustomerRecords);
    }

    [Fact]
    public async Task Importing_twice_never_duplicates_customers_or_orders()
    {
        await Service().ImportAsync(Archive(), commit: true, default);
        var second = await Service().ImportAsync(Archive(), commit: true, default);

        Assert.Equal(0, second.NewCustomers);
        Assert.Equal(2, second.AlreadyImported);
        Assert.Equal(0, second.NewOrders);
        Assert.Equal(2, await _db.LegacyCustomerRecords.CountAsync());
        Assert.Equal(1, await _db.LegacyOrderRecords.CountAsync());
    }

    [Fact]
    public async Task Claiming_needs_a_verified_matching_email_and_credits_promised_stars_once()
    {
        await Service().ImportAsync(Archive(), commit: true, default);

        Assert.Equal(0, (await Service(verifiedEmail: null).ClaimForUserAsync("u1", default)).Linked);
        Assert.Equal(0, (await Service(verifiedEmail: "someone.else@example.com").ClaimForUserAsync("u1", default)).Linked);

        var first = await Service("old.customer@example.com").ClaimForUserAsync("u1", default);
        var again = await Service("old.customer@example.com").ClaimForUserAsync("u1", default);

        Assert.Equal(1, first.Linked);
        Assert.Equal(250, first.StarsCredited);
        Assert.Equal(0, again.Linked);
        Assert.Equal(250, (await new StarsLedgerService(_db).GetBalanceAsync("u1", default)).Available);
    }

    [Fact]
    public async Task Staff_can_link_a_record_with_a_reason_and_it_cannot_be_linked_twice()
    {
        await Service().ImportAsync(Archive(), commit: true, default);
        var record = await _db.LegacyCustomerRecords.FirstAsync(r => r.LegacyId == "C-2");

        var linked = await Service().LinkManuallyAsync(record.Id, "u2", "staff", "Verified by phone call", default);
        var second = await Service().LinkManuallyAsync(record.Id, "u3", "staff", "again", default);

        Assert.Equal(1, linked.Linked);
        Assert.Equal(0, second.Linked);
        Assert.Equal(LegacyLinkStatus.Linked, (await _db.LegacyCustomerRecords.AsNoTracking().FirstAsync(r => r.Id == record.Id)).Status);
    }
}
