using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

public sealed class CreateSupplierMappingCommandHandlerTests
{
    private static CreateSupplierMappingRequest MappingRequest(Guid productId, string externalProductId = "EXT-1") =>
        new(productId, externalProductId, null, null, 9.99m, "USD", 100);

    [Fact]
    public async Task Handle_Succeeds_ForAnEnabledSupplierWithNoExistingMapping()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new CreateSupplierMappingCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var productId = Guid.NewGuid();

        var result = await handler.Handle(new CreateSupplierMappingCommand(supplier.Id, MappingRequest(productId)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.SupplierProductMappings);
    }

    [Fact]
    public async Task Handle_Rejects_DuplicateSupplierProductPair()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new CreateSupplierMappingCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var productId = Guid.NewGuid();

        var first = await handler.Handle(new CreateSupplierMappingCommand(supplier.Id, MappingRequest(productId, "EXT-1")), CancellationToken.None);
        var second = await handler.Handle(new CreateSupplierMappingCommand(supplier.Id, MappingRequest(productId, "EXT-2")), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("Supplier.MappingAlreadyExists", second.Error.Code);
        Assert.Single(db.SupplierProductMappings); // the rejected attempt must not have written a row
    }

    [Fact]
    public async Task Handle_Allows_SameProductMappedToDifferentSuppliers()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplierA = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        var supplierB = Supplier.Create("Beta", "BETA", "Manual", SupplierAuthenticationType.None, null, 100);
        db.Suppliers.AddRange(supplierA, supplierB);
        await db.SaveChangesAsync();

        var handler = new CreateSupplierMappingCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var productId = Guid.NewGuid();

        var resultA = await handler.Handle(new CreateSupplierMappingCommand(supplierA.Id, MappingRequest(productId)), CancellationToken.None);
        var resultB = await handler.Handle(new CreateSupplierMappingCommand(supplierB.Id, MappingRequest(productId)), CancellationToken.None);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        Assert.Equal(2, db.SupplierProductMappings.Count());
    }

    [Fact]
    public async Task Handle_Rejects_MappingCreationForADisabledSupplier()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        supplier.Disable();
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new CreateSupplierMappingCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(new CreateSupplierMappingCommand(supplier.Id, MappingRequest(Guid.NewGuid())), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.SupplierDisabled", result.Error.Code);
        Assert.Empty(db.SupplierProductMappings);
    }

    [Fact]
    public async Task Handle_Rejects_UnknownSupplier()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var handler = new CreateSupplierMappingCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(new CreateSupplierMappingCommand(Guid.NewGuid(), MappingRequest(Guid.NewGuid())), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }
}
