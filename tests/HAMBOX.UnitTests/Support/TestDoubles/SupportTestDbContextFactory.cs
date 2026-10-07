using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
// FakeMembershipAccessProvider lives in HAMBOX.UnitTests.Commerce.TestDoubles and is `internal`,
// which is assembly-scoped — reusable here without duplicating it.

namespace HAMBOX.UnitTests.Support.TestDoubles;

/// <summary>
/// Spins up the real <see cref="SupportDbContext"/>/<see cref="IdentityDbContext"/> — including
/// production entity configurations and the soft-delete query filter — against EF Core's InMemory
/// provider, plus a fully wired <see cref="TicketContextBuilder"/> (reusing Commerce/Catalog's own
/// InMemory factory and the membership fake already established in Commerce/TestDoubles).
/// </summary>
internal static class SupportTestDbContextFactory
{
    public static (SupportDbContext Support, IdentityDbContext Identity, TicketContextBuilder ContextBuilder) Create()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var supportOptions = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase($"support-{databaseName}")
            .Options;
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-{databaseName}")
            .Options;

        var supportDb = new SupportDbContext(supportOptions);
        var identityDb = new IdentityDbContext(identityOptions);
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();

        var contextBuilder = new TicketContextBuilder(
            identityDb, commerceDb, catalogDb, supportDb, new FakeMembershipAccessProvider());

        return (supportDb, identityDb, contextBuilder);
    }
}
