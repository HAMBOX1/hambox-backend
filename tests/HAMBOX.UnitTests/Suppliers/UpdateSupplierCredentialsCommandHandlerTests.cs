using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

public sealed class UpdateSupplierCredentialsCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReplacesCredentials_WithoutTheCallerSupplyingAnyExistingValue()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.ApiKey, null, 100);
        supplier.UpdateCredentials("old-key", null, null, null, null, null);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new UpdateSupplierCredentialsCommandHandler(db, new FakeCurrentUserService("admin-1"));
        // The request never reads or echoes back "old-key" — the admin only supplies the new value.
        var request = new UpdateSupplierCredentialsRequest("new-key", null, null, null, null, null);

        var result = await handler.Handle(new UpdateSupplierCredentialsCommand(supplier.Id, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var reloaded = await db.Suppliers.FindAsync(supplier.Id);
        Assert.Equal("new-key", reloaded!.ApiKey);
    }

    [Fact]
    public async Task Handle_WritesAnAuditEntry_ThatRecordsWhichFieldsChanged_ButNeverTheValues()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.ApiKey, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new UpdateSupplierCredentialsCommandHandler(db, new FakeCurrentUserService("admin-1"));
        const string secretValue = "sk_live_totally_secret_12345";
        var request = new UpdateSupplierCredentialsRequest(secretValue, null, null, null, null, null);

        await handler.Handle(new UpdateSupplierCredentialsCommand(supplier.Id, request), CancellationToken.None);

        var auditEntry = Assert.Single(db.SupplierAuditLogs);
        Assert.Equal(SupplierAuditAction.CredentialsUpdated, auditEntry.Action);
        Assert.NotNull(auditEntry.DetailsJson);
        Assert.DoesNotContain(secretValue, auditEntry.DetailsJson);
        Assert.Contains("\"apiKeyChanged\":true", auditEntry.DetailsJson);
        Assert.Contains("\"passwordChanged\":false", auditEntry.DetailsJson);
    }

    [Fact]
    public async Task Handle_Rejects_UnknownSupplier()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var handler = new UpdateSupplierCredentialsCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new UpdateSupplierCredentialsCommand(Guid.NewGuid(), new UpdateSupplierCredentialsRequest(null, null, null, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }
}
