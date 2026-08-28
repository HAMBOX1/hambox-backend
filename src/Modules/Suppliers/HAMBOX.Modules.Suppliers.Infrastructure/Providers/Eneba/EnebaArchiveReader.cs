using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;

internal enum EnebaKeyExtractionOutcome
{
    /// <summary>One or more text keys were found and returned in <see cref="EnebaKeyExtractionResult.Keys"/>.</summary>
    Extracted,

    /// <summary>
    /// No entry under <c>{orderNumber}/{sellableSlug}/{shortId}/</c> exists at all — either the archive
    /// doesn't match the order it was requested for, or (transiently) the export hasn't actually finished
    /// writing this item's directory yet despite <c>O_orderExport.status</c> reporting <c>COMPLETED</c>.
    /// </summary>
    DirectoryNotFound,

    /// <summary>
    /// The item's directory exists but contains only image-format keys (<c>{keyId}.png</c>/<c>.jpg</c>) —
    /// no <c>keys.txt</c>. A genuine, documented Eneba delivery shape this integration does not attempt
    /// to extract text from (see docs/integrations/suppliers/README.md §19) — never guessed/OCR'd.
    /// </summary>
    ImageKeysOnly,
}

internal sealed record EnebaKeyExtractionResult(EnebaKeyExtractionOutcome Outcome, IReadOnlyList<string> Keys);

/// <summary>
/// Reads Eneba's key-export archive (see <see cref="EnebaHttpClient.DownloadArchiveAsync"/>) — a ZIP
/// whose every entry is encrypted with the classic ZipCrypto stream cipher (confirmed by the
/// documentation's own <c>unzip -P "&lt;email&gt;"</c> example — that flag only ever refers to the legacy
/// PKZIP cipher, never AES), password = the Eneba account's login email. .NET's built-in
/// <see cref="System.IO.Compression.ZipArchive"/> cannot decrypt encrypted entries at all, hence the
/// SharpCompress dependency — see Directory.Packages.props' comment on that package reference.
/// </summary>
internal static class EnebaArchiveReader
{
    /// <summary>
    /// Extracts <c>keys.txt</c> (one key per line, per the documented archive layout) for exactly one
    /// order item, identified by the same <c>{orderNumber}/{sellableSlug}/{shortId}/</c> path the
    /// documentation says the archive is organized by. Never returns partial/guessed data: an entry
    /// that exists but is empty, or a directory that exists with no <c>keys.txt</c> and no recognizable
    /// image-key file, is reported via <see cref="EnebaKeyExtractionResult.Outcome"/> rather than an
    /// empty success.
    /// </summary>
    public static EnebaKeyExtractionResult ExtractKeys(byte[] archiveBytes, string password, string orderNumber, string sellableSlug, string shortId)
    {
        var directoryPrefix = $"{orderNumber}/{sellableSlug}/{shortId}/";

        using var stream = new MemoryStream(archiveBytes);
        using var archive = ZipArchive.Open(stream, new ReaderOptions { Password = password });

        var entriesInDirectory = archive.Entries
            .Where(e => !e.IsDirectory && NormalizeEntryKey(e.Key).StartsWith(directoryPrefix, StringComparison.Ordinal))
            .ToList();

        if (entriesInDirectory.Count == 0)
        {
            return new EnebaKeyExtractionResult(EnebaKeyExtractionOutcome.DirectoryNotFound, []);
        }

        var keysTxtEntry = entriesInDirectory.FirstOrDefault(
            e => string.Equals(NormalizeEntryKey(e.Key), $"{directoryPrefix}keys.txt", StringComparison.OrdinalIgnoreCase));

        if (keysTxtEntry is null)
        {
            // Per the documented layout, the only other possibility is image-format keys
            // ({keyId}.png/.jpg, or {keyId}.txt for ones that "failed to decode") — none of those are
            // extracted as text here; see the type's remarks.
            return new EnebaKeyExtractionResult(EnebaKeyExtractionOutcome.ImageKeysOnly, []);
        }

        using var entryStream = keysTxtEntry.OpenEntryStream();
        using var reader = new StreamReader(entryStream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line.Trim());
            }
        }

        return lines.Count == 0
            ? new EnebaKeyExtractionResult(EnebaKeyExtractionOutcome.ImageKeysOnly, [])
            : new EnebaKeyExtractionResult(EnebaKeyExtractionOutcome.Extracted, lines);
    }

    private static string NormalizeEntryKey(string? key) => (key ?? string.Empty).Replace('\\', '/');
}
