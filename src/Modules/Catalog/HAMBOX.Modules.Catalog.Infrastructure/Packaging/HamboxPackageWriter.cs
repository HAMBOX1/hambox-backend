using System.IO.Compression;
using System.Text.Json;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Builds the <c>.hambox</c> zip: <c>manifest.json</c> + one JSON file per entity type +
/// <c>images/*</c>. Uses <see cref="ZipArchive"/> (stdlib) only — no third-party zip dependency.
/// Whole-package password protection is a separate step applied by the export job handler on the
/// bytes this returns (see <see cref="PackageCryptoService"/>); this class only owns the inner
/// zip's structure, including the optional codes-only encryption.
/// </summary>
internal sealed class HamboxPackageWriter(IPackageCryptoService crypto) : IHamboxPackageWriter
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public Task<Stream> WriteAsync(
        ParsedCatalogPackage package, bool encryptCodes, string? packagePassword, CancellationToken cancellationToken)
    {
        if (encryptCodes && string.IsNullOrWhiteSpace(packagePassword))
        {
            throw new InvalidOperationException("A package password is required to encrypt digital codes.");
        }

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, "manifest.json", new
            {
                schemaVersion = SchemaVersion,
                exportedAtUtc = DateTimeOffset.UtcNow,
                codesEncrypted = encryptCodes,
                counts = new
                {
                    categories = package.Categories.Count,
                    products = package.Products.Count,
                    optionGroups = package.OptionGroups.Count,
                    options = package.Options.Count,
                    variants = package.Variants.Count,
                    codes = package.Codes.Count,
                    supplierMappings = package.SupplierMappings.Count,
                    images = package.Images.Count,
                },
            });

            WriteJsonEntry(archive, "categories.json", package.Categories);
            WriteJsonEntry(archive, "products.json", package.Products);
            WriteJsonEntry(archive, "option-groups.json", package.OptionGroups);
            WriteJsonEntry(archive, "options.json", package.Options);
            WriteJsonEntry(archive, "variants.json", package.Variants);
            WriteJsonEntry(archive, "supplier-mappings.json", package.SupplierMappings);

            if (package.Codes.Count > 0)
            {
                var codesJson = JsonSerializer.SerializeToUtf8Bytes(package.Codes, JsonOptions);
                if (encryptCodes)
                {
                    var encrypted = crypto.Encrypt(codesJson, packagePassword!);
                    WriteBinaryEntry(archive, "codes.json.enc", encrypted);
                }
                else
                {
                    WriteBinaryEntry(archive, "codes.json", codesJson);
                }
            }

            foreach (var (relativePath, bytes) in package.Images)
            {
                WriteBinaryEntry(archive, $"images/{relativePath}", bytes);
            }
        }

        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        WriteBinaryEntry(archive, entryName, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
    }

    private static void WriteBinaryEntry(ZipArchive archive, string entryName, byte[] bytes)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(bytes, 0, bytes.Length);
    }
}
