using System.Text;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Catalog.Inventory;

/// <summary>The "get my stock back as a text file" download: real codes, only the requested status, audited.</summary>
public sealed class ExportInventoryCodesTextTests
{
    private static async Task<(ExportInventoryCodesTextQueryHandler Handler, TestCatalogDbContext Db, ProductVariant Variant, InventoryBatch Batch)> SetupAsync()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), "PSN-50 USD");
        var batch = InventoryBatch.Create(variant.Id, "Batch 1");
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        await db.SaveChangesAsync();
        return (new ExportInventoryCodesTextQueryHandler(db, new FakeCurrentUserService("admin-1")), db, variant, batch);
    }

    private static string Text(ExportInventoryCodesTextDto dto) => Encoding.UTF8.GetString(dto.Content);

    [Fact]
    public async Task DefaultsToTheUnsoldCodes_OnePerLine()
    {
        var (handler, db, variant, batch) = await SetupAsync();
        var first = DigitalInventoryCode.Create(variant.Id, batch.Id, "AAAA-1111");
        var second = DigitalInventoryCode.Create(variant.Id, batch.Id, "BBBB-2222");
        var sold = DigitalInventoryCode.Create(variant.Id, batch.Id, "SOLD-0000");
        sold.Reserve();
        sold.MarkSold(Guid.NewGuid(), Guid.NewGuid(), "user-1");
        db.DigitalInventoryCodes.AddRange(first, second, sold);
        await db.SaveChangesAsync();

        var result = await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, null, null, "203.0.113.5", "UA"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Codes saved together share a timestamp, so only the content (not the order) is guaranteed here.
        Assert.Equal(new[] { "AAAA-1111", "BBBB-2222" }, Text(result.Value).TrimEnd().Split('\n').Order());
        Assert.EndsWith("\n", Text(result.Value));
        Assert.Equal(2, result.Value.CodeCount);
        Assert.Equal("text/plain; charset=utf-8", result.Value.ContentType);
        Assert.EndsWith(".txt", result.Value.FileName);
    }

    [Fact]
    public async Task APictureCannotGoIntoATextFile_SoItIsLeftOutAndCounted()
    {
        var (handler, db, variant, batch) = await SetupAsync();
        db.DigitalInventoryCodes.AddRange(
            DigitalInventoryCode.Create(variant.Id, batch.Id, "TEXT-CODE-1"),
            DigitalInventoryCode.Create(variant.Id, batch.Id, ImageCodes.ToCodeValue("_private/code-images/a.png")));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, null, null, "ip", "ua"), CancellationToken.None);

        Assert.Equal("TEXT-CODE-1\n", Text(result.Value));
        Assert.Equal(1, result.Value.CodeCount);
        Assert.Equal(1, result.Value.PictureCount);
    }

    [Fact]
    public async Task AMultiLineEntryStaysTogether_SeparatedByABlankLine()
    {
        var (handler, db, variant, batch) = await SetupAsync();
        db.DigitalInventoryCodes.AddRange(
            DigitalInventoryCode.Create(variant.Id, batch.Id, "user1@mail.com\npass-1"),
            DigitalInventoryCode.Create(variant.Id, batch.Id, "user2@mail.com\r\npass-2"));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, null, null, "ip", "ua"), CancellationToken.None);

        Assert.Equal(
            new[] { "user1@mail.com\npass-1", "user2@mail.com\npass-2" },
            Text(result.Value).TrimEnd().Split("\n\n").Order());
        Assert.DoesNotContain("\r", Text(result.Value));
    }

    [Fact]
    public async Task CanAskForAnotherStatus_AndAnUnknownStatusIsRefused()
    {
        var (handler, db, variant, batch) = await SetupAsync();
        var sold = DigitalInventoryCode.Create(variant.Id, batch.Id, "SOLD-0000");
        sold.Reserve();
        sold.MarkSold(Guid.NewGuid(), Guid.NewGuid(), "user-1");
        db.DigitalInventoryCodes.AddRange(DigitalInventoryCode.Create(variant.Id, batch.Id, "FREE-1"), sold);
        await db.SaveChangesAsync();

        var soldOnly = await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, "sold", null, "ip", "ua"), CancellationToken.None);
        var bogus = await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, "nonsense", null, "ip", "ua"), CancellationToken.None);

        Assert.Equal("SOLD-0000\n", Text(soldOnly.Value));
        Assert.Equal(CatalogErrors.InvalidCodeStatus.Code, bogus.Error.Code);
    }

    [Fact]
    public async Task EveryDownloadIsAudited_WithWhoWhereAndWhy_ButNeverThePlaintext()
    {
        var (handler, db, variant, batch) = await SetupAsync();
        db.DigitalInventoryCodes.Add(DigitalInventoryCode.Create(variant.Id, batch.Id, "SECRET-CODE-9999"));
        await db.SaveChangesAsync();

        await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, null, " moving stock ", "203.0.113.5", "UA/1.0"), CancellationToken.None);
        await handler.Handle(new ExportInventoryCodesTextQuery(variant.Id, null, null, "203.0.113.5", "UA/1.0"), CancellationToken.None);

        var logs = db.InventoryAuditLogs.Where(l => l.Action == InventoryAuditAction.CodeExported).OrderBy(l => l.CreatedOnUtc).ToList();
        Assert.Equal(2, logs.Count);
        Assert.Equal("moving stock", logs[0].Reason);
        Assert.False(string.IsNullOrWhiteSpace(logs[1].Reason));
        Assert.Equal("admin-1", logs[0].PerformedByUserId);
        Assert.Equal("203.0.113.5", logs[0].IpAddress);
        Assert.All(logs, l => Assert.DoesNotContain("SECRET-CODE-9999", l.Details ?? string.Empty));
    }

    [Fact]
    public async Task AnUnknownVariantIsRefused()
    {
        var (handler, _, _, _) = await SetupAsync();

        var result = await handler.Handle(new ExportInventoryCodesTextQuery(Guid.NewGuid(), null, null, "ip", "ua"), CancellationToken.None);

        Assert.Equal(CatalogErrors.VariantNotFound.Code, result.Error.Code);
    }
}
