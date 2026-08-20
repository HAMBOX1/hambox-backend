using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Guards the Bamboo-specific "Default Account ID" requirement on <c>Supplier.SettingsJson</c> —
/// <c>BambooSupplierProvider.PurchaseAsync</c> otherwise only discovers a missing/invalid accountId at
/// first purchase attempt. Every non-Bamboo <c>ProviderType</c> must remain untouched: the generic
/// SettingsJson mechanism stays freeform JSON for everyone else.
/// </summary>
public sealed class UpdateSupplierSettingsCommandValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("""{"accountId":"555"}""")] // string, not number
    [InlineData("""{"accountId":0}""")]
    [InlineData("""{"accountId":-5}""")]
    public async Task BambooSupplier_InvalidOrMissingAccountId_FailsValidation(string? settingsJson)
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var validator = new UpdateSupplierSettingsCommandValidator(db);
        var result = await validator.ValidateAsync(
            new UpdateSupplierSettingsCommand(supplier.Id, new UpdateSupplierSettingsRequest(settingsJson)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task BambooSupplier_PositiveNumericAccountId_PassesValidation()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var validator = new UpdateSupplierSettingsCommandValidator(db);
        var result = await validator.ValidateAsync(
            new UpdateSupplierSettingsCommand(supplier.Id, new UpdateSupplierSettingsRequest("""{"accountId":555}""")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task BambooProviderTypeMatch_IsCaseInsensitive()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var validator = new UpdateSupplierSettingsCommandValidator(db);
        var result = await validator.ValidateAsync(
            new UpdateSupplierSettingsCommand(supplier.Id, new UpdateSupplierSettingsRequest(null)));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("""{"anything":"goes"}""")]
    public async Task NonBambooSupplier_AnySettingsJson_PassesValidation(string? settingsJson)
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme Codes", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var validator = new UpdateSupplierSettingsCommandValidator(db);
        var result = await validator.ValidateAsync(
            new UpdateSupplierSettingsCommand(supplier.Id, new UpdateSupplierSettingsRequest(settingsJson)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task UnknownSupplierId_PassesValidation_NotFoundIsHandledByTheCommandHandler()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var validator = new UpdateSupplierSettingsCommandValidator(db);

        var result = await validator.ValidateAsync(
            new UpdateSupplierSettingsCommand(Guid.NewGuid(), new UpdateSupplierSettingsRequest(null)));

        Assert.True(result.IsValid);
    }
}
