using HAMBOX.API.Extensions;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Security;

/// <summary>
/// Audit R17: the read-everything decrypt probe, against real SQL Server. Rows written under one key ring are
/// reported "readable" while that ring is in place, flip to "unreadable" the moment it is lost or replaced, and
/// values that were never encrypted are surfaced separately — and no secret ever appears in the report.
/// </summary>
public sealed class EncryptionHealthProbeTests : IAsyncLifetime
{
    private const string PlainCode = "SUPER-SECRET-CODE-1234";

    private readonly string _databaseName = $"HamboxEncryptionProbe_{Guid.NewGuid():N}";
    private string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    /// <summary>"Encrypts" with a key id: a value written under key A cannot be read back under key B (a lost key ring).</summary>
    private sealed class KeyedProtector(string keyId) : ICodeProtector
    {
        public string Protect(string plainText) =>
            "CfDJ" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(keyId + "|" + plainText));

        public string Unprotect(string cipherText)
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText["CfDJ".Length..]));
            var separator = decoded.IndexOf('|');
            if (separator < 0 || decoded[..separator] != keyId)
            {
                throw new System.Security.Cryptography.CryptographicException("The key was not found in the key ring.");
            }

            return decoded[(separator + 1)..];
        }
    }

    private ServiceProvider BuildServices(ICodeProtector protector)
    {
        var services = new ServiceCollection();
        services.AddSingleton(protector);
        services.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(ConnectionString));
        services.AddDbContext<CommerceDbContext>(o => o.UseSqlServer(ConnectionString));
        services.AddDbContext<SuppliersDbContext>(o => o.UseSqlServer(ConnectionString));
        return services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        using var provider = BuildServices(new KeyedProtector("key-A"));
        await provider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<CommerceDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<SuppliersDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        using var provider = BuildServices(new KeyedProtector("key-A"));
        await provider.GetRequiredService<CatalogDbContext>().Database.EnsureDeletedAsync();
    }

    private async Task SeedAsync()
    {
        using var provider = BuildServices(new KeyedProtector("key-A"));

        var catalog = provider.GetRequiredService<CatalogDbContext>();
        var category = Category.Create("فئة", "Category", $"cat-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "d", "d", 10m, category.Id);
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}"[..20]);
        var batch = InventoryBatch.Create(variant.Id, "Batch");
        catalog.Categories.Add(category);
        catalog.Products.Add(product);
        catalog.ProductVariants.Add(variant);
        catalog.InventoryBatches.Add(batch);
        catalog.DigitalInventoryCodes.AddRange(
            DigitalInventoryCode.Create(variant.Id, batch.Id, PlainCode),
            DigitalInventoryCode.Create(variant.Id, batch.Id, "ANOTHER-CODE-5678"));
        await catalog.SaveChangesAsync();
    }

    [Fact]
    public async Task EveryRowIsReadable_WhileTheKeyRingIsIntact_AndNoSecretIsInTheReport()
    {
        await SeedAsync();
        using var provider = BuildServices(new KeyedProtector("key-A"));

        var report = await EncryptionHealthEndpoints.ProbeAsync(
            provider, provider.GetRequiredService<ICodeProtector>(), sample: null, CancellationToken.None);

        Assert.Equal("Healthy", report.Status);
        var codes = Assert.Single(report.Sources, s => s.Source == "Inventory codes");
        Assert.Equal(2, codes.CheckedRows);
        Assert.Equal(2, codes.Readable);
        Assert.Equal(0, codes.Unreadable);
        Assert.DoesNotContain(PlainCode, System.Text.Json.JsonSerializer.Serialize(report));
    }

    [Fact]
    public async Task ALostOrReplacedKeyRing_IsReportedAsDegraded_WithTheUnreadableRowCount()
    {
        await SeedAsync();
        using var provider = BuildServices(new KeyedProtector("key-B")); // the ring that "replaced" key A

        var report = await EncryptionHealthEndpoints.ProbeAsync(
            provider, provider.GetRequiredService<ICodeProtector>(), sample: null, CancellationToken.None);

        Assert.Equal("Degraded", report.Status);
        var codes = Assert.Single(report.Sources, s => s.Source == "Inventory codes");
        Assert.Equal(2, codes.Unreadable);
        Assert.Equal(0, codes.Readable);
    }

    [Fact]
    public async Task ValuesStoredInTheClear_AreCountedSeparately_NotAsHealthyOrBroken()
    {
        await SeedAsync();
        await using (var raw = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString))
        {
            await raw.OpenAsync();
            await using var cmd = raw.CreateCommand();
            cmd.CommandText = "UPDATE TOP (1) catalog.DigitalInventoryCodes SET DigitalCode = 'legacy-plaintext-value'";
            await cmd.ExecuteNonQueryAsync();
        }

        using var provider = BuildServices(new KeyedProtector("key-A"));
        var report = await EncryptionHealthEndpoints.ProbeAsync(
            provider, provider.GetRequiredService<ICodeProtector>(), sample: null, CancellationToken.None);

        var codes = Assert.Single(report.Sources, s => s.Source == "Inventory codes");
        Assert.Equal(1, codes.StoredAsPlaintext);
        Assert.Equal(1, codes.Readable);
        Assert.Equal("Healthy", report.Status);
    }

    [Fact]
    public async Task SampleLimitsHowManyRowsAreChecked()
    {
        await SeedAsync();
        using var provider = BuildServices(new KeyedProtector("key-A"));

        var report = await EncryptionHealthEndpoints.ProbeAsync(
            provider, provider.GetRequiredService<ICodeProtector>(), sample: 1, CancellationToken.None);

        Assert.True(report.Sampled);
        Assert.Equal(1, Assert.Single(report.Sources, s => s.Source == "Inventory codes").CheckedRows);
    }
}
