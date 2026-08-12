using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Catalog;

/// <summary>
/// Real SQL Server (LocalDB) coverage for ProductVariant permanent-delete concurrency —
/// deliberately NOT EF Core's InMemory provider, which throws on both <c>Database.BeginTransactionAsync</c>
/// and <c>Database.SqlQuery</c> (raw SQL), so it cannot execute
/// <see cref="InventoryEngine.DeleteVariantPermanentlyAsync"/>'s transaction/row-lock code path at
/// all — see that method's own doc comment. Requires a local SQL Server LocalDB instance
/// ("(localdb)\MSSQLLocalDB") to already be running; skips (via a clear failure) if unreachable
/// rather than silently passing.
/// </summary>
public sealed class VariantPermanentDeleteConcurrencyTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HamboxVariantConcurrency_{Guid.NewGuid():N}";
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

    private CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new CatalogDbContext(options, new PassthroughCodeProtector());
    }

    private static InventoryEngine CreateEngine(CatalogDbContext db, ICommerceVariantUsageProvider commerceUsage) =>
        new(db, new FixedCurrentUser("admin-1"), new ThrowingPlatformSettingsProvider(), commerceUsage);

    private async Task<(Category Category, Product Product, ProductVariant Variant)> SeedProductAndVariantAsync()
    {
        await using var db = CreateContext();
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();

        db.Categories.Add(category);
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        return (category, product, variant);
    }

    /// <summary>
    /// Verifies the actual locking primitive: <c>WITH (UPDLOCK, HOLDLOCK)</c> on the variant's key
    /// range in <c>catalog.DigitalInventoryCodes</c> blocks a concurrent INSERT for the same
    /// VariantId until the lock holder's transaction ends — the exact mechanism
    /// InventoryEngine.DeleteVariantPermanentlyAsync relies on to keep its usage re-check
    /// trustworthy against a concurrent writer. A fixed delay stands in for the (normally much
    /// shorter) real gap between acquiring the lock and committing, so the block is measurable
    /// rather than racing against sub-millisecond timing.
    /// </summary>
    [Fact]
    public async Task RangeLock_BlocksConcurrentInsertForSameVariant_UntilHolderTransactionEnds()
    {
        var (_, _, variant) = await SeedProductAndVariantAsync();
        var delay = TimeSpan.FromMilliseconds(800);

        DateTimeOffset holderCommittedAt = default;
        DateTimeOffset writerCompletedAt = default;
        var holderHasLock = new TaskCompletionSource();

        var holderTask = Task.Run(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var lockCmd = connection.CreateCommand())
            {
                lockCmd.Transaction = (SqlTransaction)transaction;
                lockCmd.CommandText =
                    "SELECT Id FROM catalog.DigitalInventoryCodes WITH (UPDLOCK, HOLDLOCK) WHERE VariantId = @variantId";
                lockCmd.Parameters.AddWithValue("@variantId", variant.Id);
                await using var reader = await lockCmd.ExecuteReaderAsync();
                await reader.ReadAsync(); // exhausts the (empty) result set, taking the range lock
            }

            holderHasLock.SetResult();
            await Task.Delay(delay);

            await transaction.CommitAsync();
            holderCommittedAt = DateTimeOffset.UtcNow;
        });

        var writerTask = Task.Run(async () =>
        {
            await holderHasLock.Task; // don't even attempt the insert until the holder has the lock
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO catalog.DigitalInventoryCodes
                    (Id, VariantId, BatchId, DigitalCode, CodeHash, Currency, Status, CreatedOnUtc)
                VALUES
                    (@id, @variantId, @batchId, @code, @hash, 'USD', 0, SYSDATETIMEOFFSET())
                """;
            insertCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            insertCmd.Parameters.AddWithValue("@variantId", variant.Id);
            insertCmd.Parameters.AddWithValue("@batchId", Guid.NewGuid());
            insertCmd.Parameters.AddWithValue("@code", "RACE-CODE-1");
            insertCmd.Parameters.AddWithValue("@hash", Guid.NewGuid().ToString("N"));
            await insertCmd.ExecuteNonQueryAsync();
            writerCompletedAt = DateTimeOffset.UtcNow;
        });

        await Task.WhenAll(holderTask, writerTask);

        Assert.True(
            writerCompletedAt >= holderCommittedAt,
            $"Writer completed at {writerCompletedAt:O}, before the lock holder committed at {holderCommittedAt:O} — " +
            "the range lock did not block the concurrent insert as intended.");
    }

    /// <summary>
    /// End-to-end against real SQL Server: a variant with zero usage is permanently deleted
    /// successfully by the real transactional method (not the InMemory-bypassed path).
    /// </summary>
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_RealSqlServer_NoUsage_Succeeds()
    {
        var (_, _, variant) = await SeedProductAndVariantAsync();
        var commerceUsage = new FixedCommerceVariantUsageProvider();

        await using var db = CreateContext();
        var engine = CreateEngine(db, commerceUsage);
        await engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var reloaded = await verifyDb.ProductVariants.IgnoreQueryFilters().SingleAsync(v => v.Id == variant.Id);
        Assert.True(reloaded.IsDeleted);
    }

    /// <summary>
    /// End-to-end against real SQL Server: protected history (a sold code) always wins — the real
    /// transactional method rolls back and leaves the variant untouched.
    /// </summary>
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_RealSqlServer_SoldCode_BlockedAndRolledBack()
    {
        var (_, _, variant) = await SeedProductAndVariantAsync();
        var commerceUsage = new FixedCommerceVariantUsageProvider();

        await using (var seedDb = CreateContext())
        {
            var batch = InventoryBatch.Create(variant.Id, "Batch 1", null, null, "USD", 0m, null, null);
            seedDb.InventoryBatches.Add(batch);
            var code = DigitalInventoryCode.Create(variant.Id, batch.Id, "SOLD-CODE-1");
            code.Reserve();
            code.MarkSold(Guid.NewGuid(), Guid.NewGuid());
            seedDb.DigitalInventoryCodes.Add(code);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var engine = CreateEngine(db, commerceUsage);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None));
        Assert.Equal("Variant has protected usage.", ex.Message);

        await using var verifyDb = CreateContext();
        var reloaded = await verifyDb.ProductVariants.SingleAsync(v => v.Id == variant.Id);
        Assert.False(reloaded.IsDeleted);
        // The rollback must be real — the audit log the method would have written never persisted.
        Assert.False(await verifyDb.InventoryAuditLogs.AnyAsync(l => l.VariantId == variant.Id));
    }

    private sealed class PassthroughCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;
        public string Unprotect(string cipherText) => cipherText;
    }

    private sealed class FixedCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class ThrowingPlatformSettingsProvider : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by this test.");
        public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<bool> VerifyMaintenanceBypassPasswordAsync(string password, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public void InvalidateCache(string? categoryKey = null) => throw NotNeeded();
    }

    private sealed class FixedCommerceVariantUsageProvider : ICommerceVariantUsageProvider
    {
        public Task<CommerceVariantUsageSnapshot> GetUsageAsync(Guid variantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommerceVariantUsageSnapshot(0, 0, 0));

        public Task<int> RemoveCartItemsAsync(Guid variantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
