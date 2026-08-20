using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Suppliers.TestDoubles;

internal static class SuppliersTestDbContextFactory
{
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
        var options = new DbContextOptionsBuilder<SuppliersDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new SuppliersDbContext(options, protector);
    }
}
