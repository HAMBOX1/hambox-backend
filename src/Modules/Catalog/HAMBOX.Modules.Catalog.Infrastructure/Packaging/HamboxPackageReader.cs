using System.IO.Compression;
using System.Text.Json;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Reverse of <see cref="HamboxPackageWriter"/>. Also unwraps a whole-package password-protected
/// file: <see cref="PackageMagic.WholePackage"/> as the first 4 bytes marks the rest of the stream
/// as one AES-GCM envelope (see <see cref="PackageCryptoService"/>) around the real zip.
/// </summary>
internal sealed class HamboxPackageReader(IPackageCryptoService crypto) : IHamboxPackageReader
{
    public async Task<ParsedCatalogPackage> ReadAsync(
        Stream packageStream, string? packagePassword, CancellationToken cancellationToken)
    {
        using var raw = new MemoryStream();
        await packageStream.CopyToAsync(raw, cancellationToken);
        var bytes = raw.ToArray();

        if (PackageMagic.IsWholePackageEncrypted(bytes))
        {
            if (string.IsNullOrWhiteSpace(packagePassword))
            {
                throw new UnauthorizedAccessException("This package is password-protected.");
            }

            bytes = crypto.Decrypt(bytes[PackageMagic.Length..], packagePassword);
        }

        using var zipStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var categories = ReadJsonEntry<List<ParsedCategoryRow>>(archive, "categories.json") ?? [];
        var products = ReadJsonEntry<List<ParsedProductRow>>(archive, "products.json") ?? [];
        var optionGroups = ReadJsonEntry<List<ParsedOptionGroupRow>>(archive, "option-groups.json") ?? [];
        var options = ReadJsonEntry<List<ParsedOptionRow>>(archive, "options.json") ?? [];
        var variants = ReadJsonEntry<List<ParsedVariantRow>>(archive, "variants.json") ?? [];
        var supplierMappings = ReadJsonEntry<List<ParsedSupplierMappingRow>>(archive, "supplier-mappings.json") ?? [];

        var codesEncrypted = archive.GetEntry("codes.json.enc") is not null;
        List<ParsedCodeRow> codes;
        if (codesEncrypted)
        {
            if (string.IsNullOrWhiteSpace(packagePassword))
            {
                throw new UnauthorizedAccessException("This package's digital codes are encrypted.");
            }

            var encryptedBytes = ReadBinaryEntry(archive, "codes.json.enc")
                ?? throw new InvalidDataException("codes.json.enc entry is empty.");
            var decrypted = crypto.Decrypt(encryptedBytes, packagePassword);
            codes = JsonSerializer.Deserialize<List<ParsedCodeRow>>(decrypted) ?? [];
        }
        else
        {
            codes = ReadJsonEntry<List<ParsedCodeRow>>(archive, "codes.json") ?? [];
        }

        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("images/", StringComparison.Ordinal)))
        {
            var relativePath = entry.FullName["images/".Length..];
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer, cancellationToken);
            images[relativePath] = buffer.ToArray();
        }

        return new ParsedCatalogPackage(
            categories, products, optionGroups, options, variants, codes, supplierMappings, images, codesEncrypted);
    }

    private static T? ReadJsonEntry<T>(ZipArchive archive, string entryName)
    {
        var bytes = ReadBinaryEntry(archive, entryName);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }

    private static byte[]? ReadBinaryEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

/// <summary>Magic header distinguishing a whole-package-encrypted <c>.hambox</c> file from a plain zip.</summary>
internal static class PackageMagic
{
    public static readonly byte[] WholePackage = "HBXE"u8.ToArray();

    public static int Length => WholePackage.Length;

    public static bool IsWholePackageEncrypted(byte[] bytes) =>
        bytes.Length > Length && bytes.AsSpan(0, Length).SequenceEqual(WholePackage);
}
