using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// The "Confirm N Mappings" bulk action — reuses <see cref="SupplierMappingCreator"/>, the exact same
/// validation/duplicate-check the single-create handler uses, so partial success (one bad row among
/// several valid ones) behaves identically to what a sequence of single creates would produce, just in
/// one round trip.
/// </summary>
public sealed class BulkCreateSupplierMappingsCommandHandlerTests
{
    private static CreateSupplierMappingRequest MappingRequest(Guid productId, string externalProductId) =>
        new(productId, externalProductId, null, null, 9.99m, "USD", 100);

    [Fact]
    public async Task Handle_AllValid_CreatesEveryMapping_InOneSaveChanges()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new BulkCreateSupplierMappingsCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var requests = new[]
        {
            MappingRequest(Guid.NewGuid(), "EXT-1"),
            MappingRequest(Guid.NewGuid(), "EXT-2"),
            MappingRequest(Guid.NewGuid(), "EXT-3"),
        };

        var result = await handler.Handle(new BulkCreateSupplierMappingsCommand(supplier.Id, requests), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.CreatedMappingIds.Count);
        Assert.Empty(result.Value.Failures);
        Assert.Equal(3, db.SupplierProductMappings.Count());
    }

    [Fact]
    public async Task Handle_OneDuplicateAmongValidRows_PartiallySucceeds()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        var existingProductId = Guid.NewGuid();
        db.SupplierProductMappings.Add(SupplierProductMapping.Create(supplier.Id, existingProductId, "ALREADY-MAPPED", null, null, 5m, "USD", 100));
        await db.SaveChangesAsync();

        var handler = new BulkCreateSupplierMappingsCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var requests = new[]
        {
            MappingRequest(Guid.NewGuid(), "EXT-NEW-1"),
            MappingRequest(existingProductId, "EXT-NEW-2"), // duplicate: same (supplier, product, null variant)
        };

        var result = await handler.Handle(new BulkCreateSupplierMappingsCommand(supplier.Id, requests), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.CreatedMappingIds);
        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal("Supplier.MappingAlreadyExists", failure.ErrorCode);
        Assert.Equal(2, db.SupplierProductMappings.Count()); // 1 pre-existing + 1 newly created
    }

    [Fact]
    public async Task Handle_TwoRequestsTargetSameProductInOneBatch_SecondIsRejectedAsInBatchDuplicate()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var productId = Guid.NewGuid();
        var handler = new BulkCreateSupplierMappingsCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var requests = new[]
        {
            MappingRequest(productId, "EXT-A"),
            MappingRequest(productId, "EXT-B"), // same product, no variant — collides with the row above, not the DB
        };

        var result = await handler.Handle(new BulkCreateSupplierMappingsCommand(supplier.Id, requests), CancellationToken.None);

        Assert.Single(result.Value.CreatedMappingIds);
        Assert.Single(result.Value.Failures);
        Assert.Single(db.SupplierProductMappings);
    }

    [Fact]
    public async Task Handle_DisabledSupplier_RejectsWholeBatch()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        supplier.Disable();
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var handler = new BulkCreateSupplierMappingsCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var result = await handler.Handle(
            new BulkCreateSupplierMappingsCommand(supplier.Id, [MappingRequest(Guid.NewGuid(), "EXT-1")]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.SupplierDisabled", result.Error.Code);
        Assert.Empty(db.SupplierProductMappings);
    }
}
