using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

internal static class TestCatalogDbContextFactory
{
    public static TestCatalogDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestCatalogDbContext>()
            .UseInMemoryDatabase($"variant-lifecycle-{Guid.NewGuid():N}")
            .Options;
        return new TestCatalogDbContext(options);
    }
}
