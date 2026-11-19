using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory;

public sealed class ParentProductTests
{
    [Fact]
    public async Task AUnknownProductIsRefused()
    {
        var db = TestCatalogDbContextFactory.Create();

        var result = await new SetProductParentCommandHandler(db).Handle(new SetProductParentCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ProductNotFound, result.Error);
    }

    [Fact]
    public async Task MarksAProductAsParent_AndBackToNormal_WithoutTouchingItsPrice()
    {
        var db = TestCatalogDbContextFactory.Create();
        var product = Product.Create("منتج", "PlayStation Plus", "d", "d", 0m, Guid.NewGuid());
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var handler = new SetProductParentCommandHandler(db);

        await handler.Handle(new SetProductParentCommand(product.Id, true), CancellationToken.None);
        Assert.True((await db.Products.SingleAsync(p => p.Id == product.Id)).IsParentProduct);

        await handler.Handle(new SetProductParentCommand(product.Id, false), CancellationToken.None);
        var saved = await db.Products.SingleAsync(p => p.Id == product.Id);
        Assert.False(saved.IsParentProduct);
        Assert.Equal(0m, saved.Price);
    }
}
