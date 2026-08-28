using HAMBOX.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// Empty in-memory <see cref="LegalDbContext"/> for checkout-handler tests that now take
/// <c>ILegalDbContext</c> (policy-acceptance logging). With no <c>LegalSection</c> rows seeded,
/// <c>LegalAcceptanceRecorder.RecordAsync</c> is a harmless no-op — nothing requires acceptance, so
/// it never adds a row. Tests that specifically want to assert on acceptance rows can seed sections
/// on the returned context before running the handler.
/// </summary>
internal static class LegalTestDbContextFactory
{
    public static LegalDbContext Create()
    {
        var options = new DbContextOptionsBuilder<LegalDbContext>()
            .UseInMemoryDatabase($"legal-{Guid.NewGuid():N}")
            .Options;
        return new LegalDbContext(options);
    }
}
