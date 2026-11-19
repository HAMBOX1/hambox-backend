using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Features.Customers;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>The admin Customers screen: search, member filter and the store activity shown next to each account.</summary>
public sealed class GetCustomersTests
{
    private sealed class FakeActivity(IReadOnlyDictionary<string, CustomerActivity> activity, IReadOnlySet<string> members) : ICustomerActivityReader
    {
        public Task<IReadOnlyDictionary<string, CustomerActivity>> GetActivityAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(activity);

        public Task<IReadOnlySet<string>> GetActiveMemberUserIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(members);
    }

    private static async Task<(IdentityDbContext Db, ApplicationUser Ann, ApplicationUser Bob)> SeedAsync()
    {
        var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var ann = ApplicationUser.Create("ann@example.com", "x", "Ann", "Smith");
        var bob = ApplicationUser.Create("bob@example.com", "x", "Bob", "Jones");
        bob.UpdateProfile("Bob", "Jones", "+201001112222");
        db.Users.AddRange(ann, bob);
        await db.SaveChangesAsync();
        return (db, ann, bob);
    }

    private static CustomerActivity Activity(int orders = 3, decimal spend = 42.5m, int points = 120) =>
        new(orders, orders, spend, DateTimeOffset.UtcNow, "Elite 6 months", "Active", DateTime.UtcNow.AddDays(30), points, "ANN123", "Silver");

    [Fact]
    public async Task ListsCustomersWithTheirStoreActivity()
    {
        var (db, ann, _) = await SeedAsync();
        var handler = new GetCustomersQueryHandler(db, new FakeActivity(new Dictionary<string, CustomerActivity> { [ann.Id.ToString()] = Activity() }, new HashSet<string>()));

        var result = await handler.Handle(new GetCustomersQuery(1, 25, null, null, null, null, null), CancellationToken.None);

        Assert.Equal(2, result.Value.TotalCount);
        var row = result.Value.Items.Single(c => c.Email == "ann@example.com");
        Assert.Equal(3, row.OrderCount);
        Assert.Equal(42.5m, row.PaidSpendUsd);
        Assert.Equal(120, row.PointsBalance);
        Assert.Equal("ANN123", row.ReferralCode);
        Assert.Equal(0, result.Value.Items.Single(c => c.Email == "bob@example.com").OrderCount);
    }

    [Fact]
    public async Task SearchesByEmailNameOrPhone()
    {
        var (db, _, _) = await SeedAsync();
        var handler = new GetCustomersQueryHandler(db, new FakeActivity(new Dictionary<string, CustomerActivity>(), new HashSet<string>()));

        Assert.Single((await handler.Handle(new GetCustomersQuery(1, 25, "ann@", null, null, null, null), CancellationToken.None)).Value.Items);
        Assert.Single((await handler.Handle(new GetCustomersQuery(1, 25, "Jones", null, null, null, null), CancellationToken.None)).Value.Items);
        Assert.Single((await handler.Handle(new GetCustomersQuery(1, 25, "1112222", null, null, null, null), CancellationToken.None)).Value.Items);
        Assert.Empty((await handler.Handle(new GetCustomersQuery(1, 25, "nobody", null, null, null, null), CancellationToken.None)).Value.Items);
    }

    [Fact]
    public async Task FiltersByActiveMembership()
    {
        var (db, ann, bob) = await SeedAsync();
        var handler = new GetCustomersQueryHandler(db, new FakeActivity(new Dictionary<string, CustomerActivity>(), new HashSet<string> { ann.Id.ToString() }));

        var members = await handler.Handle(new GetCustomersQuery(1, 25, null, null, true, null, null), CancellationToken.None);
        var others = await handler.Handle(new GetCustomersQuery(1, 25, null, null, false, null, null), CancellationToken.None);

        Assert.Equal(["ann@example.com"], members.Value.Items.Select(c => c.Email));
        Assert.Equal([bob.Email], others.Value.Items.Select(c => c.Email));
    }
}
