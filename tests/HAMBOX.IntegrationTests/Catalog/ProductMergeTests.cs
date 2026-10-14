using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Instructions;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.IntegrationTests.Catalog;

/// <summary>
/// Product merge (audit R14) against a real SQL Server (LocalDB) — the move path uses set-based updates that the
/// in-memory provider cannot run. Covers: simple products becoming variants, products that already carry variants
/// (stock, supplier links, cart rows and instructions follow the variant), naming, and the refusals.
/// </summary>
public sealed class ProductMergeTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HamboxProductMerge_{Guid.NewGuid():N}";
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

    private static Product NewProduct(string name, decimal price, Guid categoryId) =>
        Product.Create(name, name, "d", "d", price, categoryId);

    private sealed record Seed(Guid CategoryId, Product Target, Product Xbox1, Product Xbox2);

    private async Task<Seed> SeedAsync()
    {
        await using var db = CreateContext();
        var category = Category.Create("فئة", "Category", $"cat-{Guid.NewGuid():N}");
        var target = NewProduct("Xbox Gift Card", 10m, category.Id);
        var xbox1 = NewProduct("Xbox 1 EUR", 1.2m, category.Id);
        var xbox2 = NewProduct("Xbox 2 EUR", 2.4m, category.Id);
        db.Categories.Add(category);
        db.Products.AddRange(target, xbox1, xbox2);
        await db.SaveChangesAsync();
        return new Seed(category.Id, target, xbox1, xbox2);
    }

    private (MergeProductsCommandHandler Handler, RecordingSupplierLinkMover Mover, RecordingRelocator Relocator, TransactionProbe Transaction)
        CreateHandler(CatalogDbContext db)
    {
        var mover = new RecordingSupplierLinkMover();
        var relocator = new RecordingRelocator();
        var transaction = new TransactionProbe();
        return (new MergeProductsCommandHandler(
                db, new FixedCurrentUser("admin-1"), transaction, mover, relocator, NullLogger<MergeProductsCommandHandler>.Instance),
            mover, relocator, transaction);
    }

    [Fact]
    public async Task SimpleProducts_BecomeVariantsOfTheTarget_AndTheSourcesDisappearFromTheCatalog()
    {
        var seed = await SeedAsync();
        await using var db = CreateContext();
        var (handler, mover, _, transaction) = CreateHandler(db);

        var result = await handler.Handle(
            new MergeProductsCommand(seed.Target.Id, [seed.Xbox1.Id, seed.Xbox2.Id], ConfirmStockLoss: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.CreatedVariantIds.Count);
        Assert.Equal(2, await db.ProductVariants.CountAsync(v => v.ProductId == seed.Target.Id));
        Assert.Equal(1, await db.Products.CountAsync(p => p.Id == seed.Target.Id));
        Assert.Equal(0, await db.Products.CountAsync(p => p.Id == seed.Xbox1.Id || p.Id == seed.Xbox2.Id));
        Assert.Equal(0, transaction.Runs); // nothing moved, so no cross-module transaction was needed
        Assert.Empty(mover.Moves);
    }

    [Fact]
    public async Task ProductsThatAlreadyHaveVariants_AreMergedByMovingThem_WithCodesLinksAndInstructions()
    {
        var seed = await SeedAsync();
        Guid variantId, codeId, instructionsId;
        await using (var setup = CreateContext())
        {
            var variant = ProductVariant.Create(seed.Xbox1.Id, $"XBOX1-{Guid.NewGuid():N}"[..20]);
            variant.Activate();
            var batch = InventoryBatch.Create(variant.Id, "Batch 1");
            var code = DigitalInventoryCode.Create(variant.Id, batch.Id, "XBOX-CODE-0001");
            var instructions = ProductInstructions.CreateDraft(seed.Xbox1.Id, variant.Id, "How to redeem", "<p>steps</p>");
            setup.ProductVariants.Add(variant);
            setup.InventoryBatches.Add(batch);
            setup.DigitalInventoryCodes.Add(code);
            setup.ProductInstructions.Add(instructions);
            await setup.SaveChangesAsync();
            variantId = variant.Id;
            codeId = code.Id;
            instructionsId = instructions.Id;
        }

        await using var db = CreateContext();
        var (handler, mover, relocator, transaction) = CreateHandler(db);

        var result = await handler.Handle(
            new MergeProductsCommand(seed.Target.Id, [seed.Xbox1.Id], ConfirmStockLoss: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);

        await using var verify = CreateContext();
        var moved = await verify.ProductVariants.Include(v => v.SelectedOptions).SingleAsync(v => v.Id == variantId);
        Assert.Equal(seed.Target.Id, moved.ProductId);
        Assert.Equal(1.2m, moved.PriceOverride); // it keeps the old product's price, not the new parent's
        var optionId = Assert.Single(moved.SelectedOptions).OptionId;
        var option = await verify.ProductOptions.SingleAsync(o => o.Id == optionId);
        Assert.Equal("Xbox 1 EUR", option.Label);

        // Codes stay attached to the very same variant — nothing was copied or re-created.
        Assert.Equal(variantId, (await verify.DigitalInventoryCodes.SingleAsync(c => c.Id == codeId)).VariantId);
        Assert.Equal(seed.Target.Id, (await verify.ProductInstructions.SingleAsync(i => i.Id == instructionsId)).ProductId);

        // The cross-module follow-ups ran, inside one transaction, for exactly this variant.
        Assert.Equal(1, transaction.Runs);
        var move = Assert.Single(mover.Moves);
        Assert.Equal((variantId, seed.Xbox1.Id, seed.Target.Id), (move.VariantId, move.FromProductId, move.ToProductId));
        Assert.Contains((variantId, seed.Target.Id), relocator.Moved);
    }

    [Fact]
    public async Task ASourceWithSeveralVariants_GetsOneOptionPerVariant_NamedAfterThem()
    {
        var seed = await SeedAsync();
        await using (var setup = CreateContext())
        {
            var group = ProductOptionGroup.Create(seed.Xbox2.Id, "platform", "Platform");
            var pc = group.AddOption("pc", "PC", 0);
            var console = group.AddOption("console", "Console", 1);
            var v1 = ProductVariant.Create(seed.Xbox2.Id, $"X2PC-{Guid.NewGuid():N}"[..20]);
            v1.SetOptions([pc.Id]);
            var v2 = ProductVariant.Create(seed.Xbox2.Id, $"X2CO-{Guid.NewGuid():N}"[..20]);
            v2.SetOptions([console.Id]);
            setup.ProductOptionGroups.Add(group);
            setup.ProductOptions.AddRange(pc, console);
            setup.ProductVariants.AddRange(v1, v2);
            await setup.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var (handler, mover, _, _) = CreateHandler(db);

        var result = await handler.Handle(
            new MergeProductsCommand(seed.Target.Id, [seed.Xbox2.Id], ConfirmStockLoss: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(2, mover.Moves.Count);

        await using var verify = CreateContext();
        var labels = await verify.ProductOptionGroups
            .Where(g => g.ProductId == seed.Target.Id && g.Key == "variant")
            .SelectMany(g => g.Options.Select(o => o.Label))
            .ToListAsync();
        Assert.Contains("Xbox 2 EUR - PC", labels);
        Assert.Contains("Xbox 2 EUR - Console", labels);
    }

    [Fact]
    public async Task MergingAProductIntoItself_IsRefused()
    {
        var seed = await SeedAsync();
        await using var db = CreateContext();
        var (handler, _, _, _) = CreateHandler(db);

        var result = await handler.Handle(
            new MergeProductsCommand(seed.Target.Id, [seed.Target.Id], ConfirmStockLoss: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(1, await db.Products.CountAsync(p => p.Id == seed.Target.Id));
    }

    [Fact]
    public async Task AnUnknownSource_IsRefused_AndNothingIsChanged()
    {
        var seed = await SeedAsync();
        await using var db = CreateContext();
        var (handler, _, _, _) = CreateHandler(db);

        var result = await handler.Handle(
            new MergeProductsCommand(seed.Target.Id, [seed.Xbox1.Id, Guid.NewGuid()], ConfirmStockLoss: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, await db.ProductVariants.CountAsync(v => v.ProductId == seed.Target.Id));
        Assert.Equal(1, await db.Products.CountAsync(p => p.Id == seed.Xbox1.Id));
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
        public bool IsAdminContext => UserId is not null;
        public Guid? SessionId => null;
    }

    /// <summary>Runs the action directly on the single catalog connection and counts how many times it was used.</summary>
    internal sealed class TransactionProbe : ICatalogSuppliersTransactionService
    {
        public int Runs { get; private set; }

        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            Runs++;
            await action(cancellationToken);
        }
    }

    internal sealed class RecordingSupplierLinkMover : IVariantSupplierLinkMover
    {
        public List<VariantMove> Moves { get; } = [];

        public Task MoveAsync(IReadOnlyCollection<VariantMove> moves, CancellationToken cancellationToken)
        {
            Moves.AddRange(moves);
            return Task.CompletedTask;
        }
    }

    internal sealed class RecordingRelocator : ICommerceVariantRelocator
    {
        public List<(Guid VariantId, Guid NewProductId)> Moved { get; } = [];

        public Task MoveVariantAsync(Guid variantId, Guid newProductId, CancellationToken cancellationToken = default)
        {
            Moved.Add((variantId, newProductId));
            return Task.CompletedTask;
        }
    }
}
