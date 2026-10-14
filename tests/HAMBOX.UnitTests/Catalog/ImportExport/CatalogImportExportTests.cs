using System.Text;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.ValidateCatalogImport;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

namespace HAMBOX.UnitTests.Catalog.ImportExport;

/// <summary>
/// Audit R14: catalog import / export — the .hambox package round trip (plain, encrypted codes, password-protected),
/// CSV parsing, and the validation report an admin sees before anything is written.
/// </summary>
public sealed class CatalogImportExportTests
{
    private static readonly PackageCryptoService Crypto = new();

    private static ParsedCatalogPackage SamplePackage() => ParsedCatalogPackage.Empty with
    {
        Categories = [new ParsedCategoryRow(1, "games", "Games", "ألعاب", null, true, 0)],
        Products =
        [
            new ParsedProductRow(
                1, "steam-20", "Steam 20 USD", "ستيم 20", "Wallet code", null, 20m, "games", "Active", 5, [], [], null),
        ],
        Variants =
        [
            new ParsedVariantRow(1, "STEAM-20-US", "steam-20", 19.5m, 22m, "Active", 3, null, []),
        ],
        Codes =
        [
            new ParsedCodeRow(1, "STEAM-20-US", "AAAA-BBBB-CCCC-1111", "SN-1", null, 15m, "USD", null, "Batch A"),
            new ParsedCodeRow(2, "STEAM-20-US", "AAAA-BBBB-CCCC-2222", null, "1234", 15m, "USD", null, "Batch A"),
        ],
    };

    private static async Task<byte[]> WriteAsync(ParsedCatalogPackage package, bool encryptCodes, string? password)
    {
        var writer = new HamboxPackageWriter(Crypto);
        await using var stream = await writer.WriteAsync(package, encryptCodes, password, CancellationToken.None);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }

    private static Task<ParsedCatalogPackage> ReadAsync(byte[] bytes, string? password) =>
        new HamboxPackageReader(Crypto).ReadAsync(new MemoryStream(bytes), password, CancellationToken.None);

    // ─── .hambox round trip ─────────────────────────────────────────────

    [Fact]
    public async Task PlainPackage_RoundTripsEveryEntity()
    {
        var bytes = await WriteAsync(SamplePackage(), encryptCodes: false, password: null);

        var read = await ReadAsync(bytes, null);

        Assert.Equal("games", Assert.Single(read.Categories).Slug);
        var product = Assert.Single(read.Products);
        Assert.Equal("Steam 20 USD", product.NameEn);
        Assert.Equal(20m, product.Price);
        var variant = Assert.Single(read.Variants);
        Assert.Equal("STEAM-20-US", variant.Sku);
        Assert.Equal(19.5m, variant.PriceOverride);
        Assert.Equal(2, read.Codes.Count);
        Assert.False(read.CodesEncrypted);
    }

    [Fact]
    public async Task EncryptedCodes_NeverAppearInThePackage_AndNeedThePasswordToBeRead()
    {
        var bytes = await WriteAsync(SamplePackage(), encryptCodes: true, password: "correct horse");

        Assert.DoesNotContain("AAAA-BBBB-CCCC-1111", Encoding.UTF8.GetString(bytes));

        var read = await ReadAsync(bytes, "correct horse");
        Assert.True(read.CodesEncrypted);
        Assert.Contains(read.Codes, c => c.Code == "AAAA-BBBB-CCCC-1111");
        Assert.Contains(read.Codes, c => c.Pin == "1234");
    }

    [Fact]
    public async Task EncryptedCodes_CannotBeReadWithTheWrongPassword()
    {
        var bytes = await WriteAsync(SamplePackage(), encryptCodes: true, password: "correct horse");

        var failure = await Record.ExceptionAsync(() => ReadAsync(bytes, "wrong password"));

        Assert.NotNull(failure);
    }

    [Fact]
    public async Task EncryptingCodes_WithoutAPassword_IsRefused()
    {
        var failure = await Record.ExceptionAsync(() => WriteAsync(SamplePackage(), encryptCodes: true, password: null));

        Assert.IsType<InvalidOperationException>(failure);
    }

    // ─── CSV parsing ────────────────────────────────────────────────────

    private static async Task<ParsedCatalogPackage> ParseCsvAsync(string csv, CatalogImportEntityType entityType)
    {
        var parser = new CatalogImportParser(new HamboxPackageReader(Crypto));
        return await parser.ParseAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), CatalogPackageFormat.Csv, entityType, null, CancellationToken.None);
    }

    [Fact]
    public async Task ProductCsv_HandlesQuotedCommas_BlankPrices_AndLists()
    {
        var csv = string.Join(
            "\n",
            "NameEn,Price,CategorySlug,Status,AdditionalCategorySlugs",
            "\"Steam, 20 USD \"\"Gift\"\"\",20.5,games,Active,\"gift-cards,wallets\"",
            "Plain Product,,games,Draft,");

        var package = await ParseCsvAsync(csv, CatalogImportEntityType.Products);

        Assert.Equal(2, package.Products.Count);
        var first = package.Products[0];
        Assert.Equal("Steam, 20 USD \"Gift\"", first.NameEn);
        Assert.Equal(20.5m, first.Price);
        Assert.Equal(["gift-cards", "wallets"], first.AdditionalCategorySlugs);
        Assert.Null(package.Products[1].Price); // blank = "not provided", not zero
    }

    [Fact]
    public async Task CategoryCsv_ParsesParentsAndFlags()
    {
        const string csv = "Slug,NameEn,NameAr,ParentSlug,IsActive,SortOrder\ngames,Games,ألعاب,,true,2\nsteam,Steam,,games,false,1";

        var package = await ParseCsvAsync(csv, CatalogImportEntityType.Categories);

        Assert.Equal(2, package.Categories.Count);
        Assert.Equal("games", package.Categories[1].ParentSlug);
        Assert.False(package.Categories[1].IsActive);
        Assert.Equal(2, package.Categories[0].SortOrder);
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        var failure = await Record.ExceptionAsync(() => ParseCsvAsync(string.Empty, CatalogImportEntityType.Products));

        Assert.IsType<InvalidDataException>(failure);
    }

    // ─── validation report ──────────────────────────────────────────────

    private sealed class InMemoryUploads(string content) : IFileStorage
    {
        public long MaxFileSizeBytes => 10_000_000;

        public bool IsAllowedContentType(string contentType) => true;

        public Task<StoredFileResult> SaveAsync(
            Stream stream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    [Fact]
    public async Task Validation_FlagsBadRows_AndPlansNewCategories_BeforeAnythingIsWritten()
    {
        var db = TestCatalogDbContextFactory.Create();
        db.Categories.Add(Category.Create("ألعاب", "Games", "games"));
        var csv = string.Join(
            "\n",
            "NameEn,Price,CategorySlug,Status",
            "Good Product,10,games,Draft",
            ",10,games,Draft",
            "Bad Price,abc,games,Draft",
            "New Cat Product,10,brand-new,Draft");
        var job = CatalogPackageJob.CreateUpload(
            CatalogPackageFormat.Csv, CatalogImportEntityType.Products, "uploads/products.csv", "products.csv", csv.Length);
        db.CatalogPackageJobs.Add(job);
        await db.SaveChangesAsync();

        var handler = new ValidateCatalogImportQueryHandler(
            db, new InMemoryUploads(csv), new CatalogImportParser(new HamboxPackageReader(Crypto)));

        var result = await handler.Handle(new ValidateCatalogImportQuery(job.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var report = result.Value;

        // Two good products, plus the category the second one refers to that does not exist yet.
        Assert.Equal(2, report.Rows.Count(r => r.EntityType == "Product" && r.Status == CatalogImportRowStatus.New));
        Assert.Contains(report.Rows, r => r.EntityType == "Category" && r.Label == "brand-new" && r.Status == CatalogImportRowStatus.New);

        // A blank name and an unparseable price are rejected with a reason, never silently imported.
        Assert.Equal(2, report.InvalidCount);
        Assert.Contains(report.Rows, r => r.Status == CatalogImportRowStatus.Invalid && r.Errors.Any(e => e.Contains("NameEn is required")));
        Assert.Contains(report.Rows, r => r.Status == CatalogImportRowStatus.Invalid && r.Errors.Any(e => e.Contains("Price")));

        // Validation is read-only: nothing was written.
        Assert.Empty(db.Products);
        Assert.Single(db.Categories);
    }

    [Fact]
    public async Task Validation_OfAnUnknownUpload_FailsCleanly()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new ValidateCatalogImportQueryHandler(
            db, new InMemoryUploads(string.Empty), new CatalogImportParser(new HamboxPackageReader(Crypto)));

        var result = await handler.Handle(new ValidateCatalogImportQuery(Guid.NewGuid(), null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
