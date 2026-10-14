using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HAMBOX.UnitTests.Suppliers.TestDoubles;

internal static class SuppliersTestDbContextFactory
{
    // Without service-provider caching each context gets its own provider, and the in-memory store is per
    // provider unless a root is shared — so the write and read contexts of one test share this root.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot> Roots = new();

    /// <summary>Fresh in-memory database, credential columns pass through unmodified (no real Data Protection wiring needed for handler-level tests).</summary>
    public static SuppliersDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<SuppliersDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"suppliers-{Guid.NewGuid():N}")
            .Options;
        return new SuppliersDbContext(options, new FakeCodeProtector());
    }

    /// <summary>Same in-memory database, but with a protector that actually transforms the value — for
    /// tests proving the credential columns are round-tripped through <c>ICodeProtector</c> rather than
    /// stored/read as-is.</summary>
    public static SuppliersDbContext CreateWithRecordingProtector(string databaseName, RecordingFakeProtector protector)
    {
        // The value converters capture the protector when the EF model is built, and EF caches that model per
        // internal service provider — without this, whichever test built the model first would own the
        // converters and this test's own protector would never see a value.
        var options = new DbContextOptionsBuilder<SuppliersDbContext>()
            .UseInMemoryDatabase(databaseName, Roots.GetOrAdd(databaseName, _ => new InMemoryDatabaseRoot()))
            .EnableServiceProviderCaching(false)
            .Options;
        return new SuppliersDbContext(options, protector);
    }
}
