using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Application.Abstractions;

/// <summary>
/// Builds a <c>.hambox</c> package (a zip: manifest + one JSON file per entity type + images) from
/// an already-resolved <see cref="ParsedCatalogPackage"/>. Implemented in Infrastructure with
/// <c>System.IO.Compression.ZipArchive</c> — no third-party zip dependency.
/// </summary>
public interface IHamboxPackageWriter
{
    Task<Stream> WriteAsync(
        ParsedCatalogPackage package,
        bool encryptCodes,
        string? packagePassword,
        CancellationToken cancellationToken);
}

/// <summary>Reverse of <see cref="IHamboxPackageWriter"/> — reads a <c>.hambox</c> stream back into the same shape.</summary>
public interface IHamboxPackageReader
{
    Task<ParsedCatalogPackage> ReadAsync(Stream packageStream, string? packagePassword, CancellationToken cancellationToken);
}

/// <summary>
/// Self-contained, password-derived encryption (PBKDF2 + AES-GCM) used for both "encrypt digital
/// codes" and "password-protect the whole package". Deliberately independent of
/// <c>ICodeProtector</c> (ASP.NET Core Data Protection) — that ciphertext is tied to the server's
/// own key ring and cannot be decrypted after moving the file to a different HAMBOX installation,
/// which is the primary scenario this feature exists for.
/// </summary>
public interface IPackageCryptoService
{
    byte[] Encrypt(byte[] plaintext, string password);

    byte[] Decrypt(byte[] ciphertext, string password);
}

/// <summary>
/// Parses an uploaded file — a <c>.hambox</c> package, or a single-entity xlsx/csv template — into
/// the same <see cref="ParsedCatalogPackage"/> shape the writer produces, so Validate and Execute
/// never duplicate parsing logic.
/// </summary>
public interface ICatalogImportParser
{
    Task<ParsedCatalogPackage> ParseAsync(
        Stream fileStream,
        CatalogPackageFormat format,
        CatalogImportEntityType entityType,
        string? packagePassword,
        CancellationToken cancellationToken);
}

/// <summary>Generates a blank xlsx template (header + sample rows + an Instructions sheet) for one entity type.</summary>
public interface IImportTemplateGenerator
{
    byte[] Generate(CatalogImportEntityType entityType);
}

/// <summary>
/// Shares one raw ADO connection/transaction across <c>CatalogDbContext</c> and Suppliers'
/// <c>SuppliersDbContext</c> — the same recipe <c>ICommerceTransactionService</c> already uses for
/// Commerce+Catalog, just parameterized over a different pair of contexts. Needed only when an
/// import package includes supplier mappings, since "never leave partial imports" requires
/// Catalog writes and Suppliers writes to roll back together.
/// </summary>
public interface ICatalogSuppliersTransactionService
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
