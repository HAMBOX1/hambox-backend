using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Guards <c>SuppliersDbContext.ApplyCredentialEncryption</c> — the EF value-converter wiring that
/// encrypts <see cref="Supplier"/> credential columns at rest via <c>ICodeProtector</c>. A regression
/// here (e.g. someone removing <c>.HasConversion(...)</c> while touching the configuration) would
/// silently put credentials back to plaintext with no compile error, so this is worth pinning down.
/// </summary>
public sealed class SupplierCredentialEncryptionTests
{
    [Fact]
    public async Task Credentials_AreRunThroughTheProtector_OnSave_AndDecryptedCorrectly_OnReload()
    {
        var dbName = $"suppliers-encryption-{Guid.NewGuid():N}";
        var protector = new RecordingFakeProtector();

        Guid supplierId;
        await using (var writeContext = SuppliersTestDbContextFactory.CreateWithRecordingProtector(dbName, protector))
        {
            var supplier = Supplier.Create("Acme Codes", "ACME", "Manual", SupplierAuthenticationType.ApiKey, null, 100);
            supplier.UpdateCredentials("live-api-key-abc123", "live-secret-xyz789", "svc-account", "hunter2", "bearer-token-value", """{"clientSecret":"oauth-secret"}""");
            writeContext.Suppliers.Add(supplier);
            await writeContext.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        // Every non-null credential field must have gone through Protect() at least once.
        Assert.Contains("live-api-key-abc123", protector.ProtectedValues);
        Assert.Contains("live-secret-xyz789", protector.ProtectedValues);
        Assert.Contains("hunter2", protector.ProtectedValues);
        Assert.Contains("bearer-token-value", protector.ProtectedValues);
        Assert.Contains("""{"clientSecret":"oauth-secret"}""", protector.ProtectedValues);

        await using var readContext = SuppliersTestDbContextFactory.CreateWithRecordingProtector(dbName, protector);
        var reloaded = await readContext.Suppliers.FindAsync(supplierId);

        Assert.NotNull(reloaded);
        Assert.Equal("live-api-key-abc123", reloaded!.ApiKey);
        Assert.Equal("live-secret-xyz789", reloaded.ApiSecret);
        Assert.Equal("hunter2", reloaded.Password);
        Assert.Equal("bearer-token-value", reloaded.BearerToken);
        Assert.Equal("""{"clientSecret":"oauth-secret"}""", reloaded.OAuthSettingsJson);
        // Username is intentionally not a secret and is never passed through the protector.
        Assert.Equal("svc-account", reloaded.Username);
    }

    [Fact]
    public async Task NullCredentials_DoNotCrash_AndConverterIsNeverInvokedForThem()
    {
        var dbName = $"suppliers-encryption-nulls-{Guid.NewGuid():N}";
        var protector = new RecordingFakeProtector();

        await using var context = SuppliersTestDbContextFactory.CreateWithRecordingProtector(dbName, protector);
        var supplier = Supplier.Create("No Creds Inc", "NOCREDS", "Manual", SupplierAuthenticationType.None, null, 100);
        context.Suppliers.Add(supplier);

        await context.SaveChangesAsync();

        Assert.Empty(protector.ProtectedValues);

        var reloaded = await context.Suppliers.FindAsync(supplier.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.ApiKey);
        Assert.Null(reloaded.Password);
    }

    [Fact]
    public void SupplierDetailDto_NeverDeclaresARawSecretProperty()
    {
        // Regression guard: the DTO must only ever expose boolean "Has*" presence flags for secrets,
        // never the underlying value, no matter how the mapper/DTO shape evolves.
        var disallowedPropertyNames = new[] { "ApiKey", "ApiSecret", "Password", "BearerToken", "OAuthSettingsJson" };

        var actualPropertyNames = typeof(SupplierDetailDto).GetProperties().Select(p => p.Name).ToArray();

        foreach (var disallowed in disallowedPropertyNames)
        {
            Assert.DoesNotContain(disallowed, actualPropertyNames);
        }
    }

    [Fact]
    public void SupplierMapper_ToDetail_OnlyExposesPresenceBooleans_ForASupplierWithRealCredentials()
    {
        var supplier = Supplier.Create("Acme Codes", "ACME2", "Manual", SupplierAuthenticationType.ApiKey, null, 100);
        supplier.UpdateCredentials("super-secret-key", null, null, null, null, null);

        var dto = SupplierMapper.ToDetail(supplier);

        Assert.True(dto.HasApiKey);
        Assert.False(dto.HasApiSecret);
        // The DTO shape itself (asserted above) has no field that could carry the raw value, but this
        // also checks the mapper doesn't leak it into an unrelated field like SettingsJson.
        Assert.DoesNotContain("super-secret-key", dto.SettingsJson ?? string.Empty);
    }
}
