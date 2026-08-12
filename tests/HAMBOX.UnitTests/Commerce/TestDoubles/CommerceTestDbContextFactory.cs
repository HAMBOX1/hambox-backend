using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// Spins up the real <see cref="CommerceDbContext"/>/<see cref="CatalogDbContext"/> — including their
/// production entity configurations and query filters — against EF Core's InMemory provider. Handlers
/// under test (e.g. <c>AddCartItemCommandHandler</c>) cast <c>ICommerceDbContext</c> back to
/// <see cref="DbContext"/> in places (<c>CartPersistenceHelper</c>), so a real <see cref="DbContext"/>
/// subclass is required rather than a hand-rolled fake of the interface.
/// </summary>
internal static class CommerceTestDbContextFactory
{
    public static (CommerceDbContext Commerce, CatalogDbContext Catalog) Create()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var protector = new FakeCodeProtector();

        var commerceOptions = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"commerce-{databaseName}")
            .Options;
        var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase($"catalog-{databaseName}")
            .Options;

        return (new CommerceDbContext(commerceOptions, protector), new CatalogDbContext(catalogOptions, protector));
    }
}
