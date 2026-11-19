using System.IO.Compression;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Catalog.Inventory;

/// <summary>"Get the pictures I uploaded as stock back": a zip of the stored files, named after the uploads, audited.</summary>
public sealed class ExportInventoryPicturesTests
{
    private sealed class FakeStorage(Dictionary<string, byte[]> files) : IFileStorage
    {
        public long MaxFileSizeBytes => 10_000_000;

        public Task<StoredFileResult> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            files.TryGetValue(storageKey, out var bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes))
                : throw new FileNotFoundException(storageKey);

        public bool IsAllowedContentType(string contentType) => true;
    }

    private static async Task<(ExportInventoryPicturesQueryHandler Handler, TestCatalogDbContext Db, ProductVariant Variant, InventoryBatch Batch)> SetupAsync(Dictionary<string, byte[]> files)
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), "GIFT-50");
        var batch = InventoryBatch.Create(variant.Id, "Pictures");
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        await db.SaveChangesAsync();
        return (new ExportInventoryPicturesQueryHandler(db, new FakeStorage(files), new FakeCurrentUserService("admin-1")), db, variant, batch);
    }

    private static DigitalInventoryCode Picture(ProductVariant variant, InventoryBatch batch, string key, string uploadedName) =>
        DigitalInventoryCode.Create(variant.Id, batch.Id, ImageCodes.ToCodeValue(key), notes: uploadedName);

    [Fact]
    public async Task ZipsTheUnsoldPictures_NamedAfterTheUploads()
    {
        var key1 = ImageCodes.Folder + "/a1.png";
        var key2 = ImageCodes.Folder + "/a2.png";
        var (handler, db, variant, batch) = await SetupAsync(new() { [key1] = [1, 2, 3], [key2] = [4, 5] });
        db.DigitalInventoryCodes.AddRange(
            Picture(variant, batch, key1, "card.png"),
            Picture(variant, batch, key2, "card.png"),
            DigitalInventoryCode.Create(variant.Id, batch.Id, "TEXT-CODE"));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new ExportInventoryPicturesQuery(variant.Id, null, null, "ip", "ua"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PictureCount);
        Assert.Equal("application/zip", result.Value.ContentType);
        using var zip = new ZipArchive(new MemoryStream(result.Value.Content));
        Assert.Equal(["card-2.png", "card.png"], zip.Entries.Select(e => e.FullName).Order().ToArray());
    }

    [Fact]
    public async Task ACodeWhoseFileIsGone_IsCountedAsMissing_NotFatal()
    {
        var present = ImageCodes.Folder + "/ok.png";
        var (handler, db, variant, batch) = await SetupAsync(new() { [present] = [9] });
        db.DigitalInventoryCodes.AddRange(
            Picture(variant, batch, present, "ok.png"),
            Picture(variant, batch, ImageCodes.Folder + "/gone.png", "gone.png"));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new ExportInventoryPicturesQuery(variant.Id, null, null, "ip", "ua"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PictureCount);
        Assert.Equal(1, result.Value.MissingCount);
    }

    [Fact]
    public async Task AVariantWithoutPictureStock_IsRefused()
    {
        var (handler, db, variant, batch) = await SetupAsync([]);
        db.DigitalInventoryCodes.Add(DigitalInventoryCode.Create(variant.Id, batch.Id, "TEXT-ONLY"));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new ExportInventoryPicturesQuery(variant.Id, null, null, "ip", "ua"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.NoPictureStock, result.Error);
    }
}
