using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Catalog;

/// <summary>
/// Audit R21 on real SQL Server (LocalDB): a pending hosted-gateway order HOLDS its manual stock while the
/// customer pays, the hold is exclusive under contention, is consumed by the paid order, and is released —
/// with a recorded reason — on failure or expiry.
/// </summary>
public sealed class OrderStockHoldTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HamboxStockHold_{Guid.NewGuid():N}";
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

    private CatalogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(ConnectionString).Options, new PassthroughCodeProtector());

    private static InventoryEngine CreateEngine(CatalogDbContext db) =>
        new(db, new FixedCurrentUser("system"), new FixedInventorySettings(), new NoUsage());

    private async Task<(Guid VariantId, List<Guid> CodeIds)> SeedStockAsync(int codes)
    {
        await using var db = CreateContext();
        var category = Category.Create("فئة", "Category", $"cat-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "d", "d", 10m, category.Id);
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}"[..20]);
        variant.Activate();
        var batch = InventoryBatch.Create(variant.Id, "Batch");
        var list = Enumerable.Range(0, codes)
            .Select(i => DigitalInventoryCode.Create(variant.Id, batch.Id, $"HOLD-CODE-{Guid.NewGuid():N}"))
            .ToList();

        db.Categories.Add(category);
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        db.DigitalInventoryCodes.AddRange(list);
        await db.SaveChangesAsync();
        return (variant.Id, list.Select(c => c.Id).ToList());
    }

    [Fact]
    public async Task Hold_KeepsTheCodeAwayFromOtherBuyers_UntilItIsReleased()
    {
        var (variantId, _) = await SeedStockAsync(1);
        var orderId = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            var held = await CreateEngine(db).ReserveForOrderAsync(
                variantId, 1, "buyer-1", orderId, DateTimeOffset.UtcNow.AddMinutes(30));
            Assert.Single(held);
        }

        // Another customer's checkout finds nothing left.
        await using (var db = CreateContext())
        {
            var other = await CreateEngine(db).ReservePartialCodesAsync(variantId, 1, "buyer-2", cartId: null);
            Assert.Empty(other);
        }

        // Payment failed → hold released with a reason → the code is sellable again.
        await using (var db = CreateContext())
        {
            var released = await CreateEngine(db).ReleaseReservationsForOrderAsync(orderId, "Payment failed");
            Assert.Equal(1, released);
        }

        await using (var db = CreateContext())
        {
            var again = await CreateEngine(db).ReservePartialCodesAsync(variantId, 1, "buyer-2", cartId: null);
            Assert.Single(again);

            var reservation = await db.InventoryReservations.IgnoreQueryFilters().SingleAsync(r => r.OrderId == orderId);
            Assert.False(reservation.IsActive);
            Assert.Equal("Payment failed", reservation.ReleaseReason);
            Assert.Contains(
                await db.InventoryAuditLogs.Where(l => l.Action == InventoryAuditAction.CodeReleased).ToListAsync(),
                l => l.Details != null && l.Details.Contains("Payment failed"));
        }
    }

    [Fact]
    public async Task TwoPendingOrders_CompetingForTheLastCode_OnlyOneHoldsIt()
    {
        var (variantId, _) = await SeedStockAsync(1);

        async Task<int> TryHold(string buyer)
        {
            await using var db = CreateContext();
            var held = await CreateEngine(db).ReserveForOrderAsync(
                variantId, 1, buyer, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(30));
            return held.Count;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(() => TryHold($"buyer-{i}"))));

        Assert.Equal(1, results.Sum());
    }

    [Fact]
    public async Task PaidOrder_ClaimsItsOwnHold_AndCommitsTheSameCode()
    {
        var (variantId, codeIds) = await SeedStockAsync(2);
        var orderId = Guid.NewGuid();

        ReservedCodeSnapshot heldSnapshot;
        await using (var db = CreateContext())
        {
            heldSnapshot = Assert.Single(await CreateEngine(db).ReserveForOrderAsync(
                variantId, 1, "buyer-1", orderId, DateTimeOffset.UtcNow.AddMinutes(1)));
        }

        // Payment confirmed, even though the hold was about to lapse: claiming extends it, so the sweep cannot take it.
        await using (var db = CreateContext())
        {
            var engine = CreateEngine(db);
            var claimed = await engine.ClaimOrderHoldsAsync(orderId);
            Assert.Equal(heldSnapshot.CodeId, Assert.Single(claimed).CodeId);

            await engine.ExpireStaleReservationsAsync();

            var committed = await engine.CommitReservationsAsync(
                orderId, [(Guid.NewGuid(), claimed[0].CodeId)]);
            Assert.Single(committed);
        }

        await using (var db = CreateContext())
        {
            var code = await db.DigitalInventoryCodes.SingleAsync(c => c.Id == heldSnapshot.CodeId);
            Assert.Equal(InventoryCodeStatus.Sold, code.Status);

            var other = await db.DigitalInventoryCodes.SingleAsync(c => c.Id != heldSnapshot.CodeId && codeIds.Contains(c.Id));
            Assert.Equal(InventoryCodeStatus.Available, other.Status);
        }
    }

    [Fact]
    public async Task UnpaidHold_ExpiresOnItsOwn_AndIsLogged()
    {
        var (variantId, _) = await SeedStockAsync(1);
        var orderId = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            await CreateEngine(db).ReserveForOrderAsync(
                variantId, 1, "buyer-1", orderId, DateTimeOffset.UtcNow.AddMilliseconds(-1));
        }

        await using (var db = CreateContext())
        {
            var expired = await CreateEngine(db).ExpireStaleReservationsAsync();
            Assert.Equal(1, expired);
        }

        await using (var db = CreateContext())
        {
            var reservation = await db.InventoryReservations.SingleAsync(r => r.OrderId == orderId);
            Assert.False(reservation.IsActive);
            Assert.Equal("Reservation expired", reservation.ReleaseReason);

            var stock = await CreateEngine(db).GetVariantStockAsync(variantId);
            Assert.Equal(1, stock.Available);
        }
    }

    // ─── doubles ────────────────────────────────────────────────────────

    private sealed class PassthroughCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;
        public string Unprotect(string cipherText) => cipherText;
    }

    private sealed class FixedCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public string? DisplayName => null;
        public bool IsAuthenticated => UserId is not null;
        public bool IsAdminContext => false;
        public Guid? SessionId => null;
    }

    private sealed class NoUsage : ICommerceVariantUsageProvider
    {
        public Task<CommerceVariantUsageSnapshot> GetUsageAsync(Guid variantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommerceVariantUsageSnapshot(0, 0, 0));

        public Task<int> RemoveCartItemsAsync(Guid variantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class FixedInventorySettings : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by these tests.");

        public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new InventorySettingsPayload(5, 15, true, "Admin"));

        public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<bool> VerifyMaintenanceBypassPasswordAsync(string password, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public void InvalidateCache(string? categoryKey = null) => throw NotNeeded();
    }
}
