using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>Enqueued by <c>ExportCatalogCommandHandler</c>, consumed by the Infrastructure job handler.</summary>
public sealed record ExportCatalogJobPayload(
    Guid PackageJobId,
    IReadOnlyList<Guid> ProductIds,
    CatalogPackageFormat Format,
    CatalogPackageOptions Options,
    bool EncryptCodes,
    bool PasswordProtectPackage,
    string? PackagePassword);

/// <summary>Enqueued by <c>ExecuteCatalogImportCommandHandler</c>, consumed by the Infrastructure job handler.</summary>
public sealed record ExecuteCatalogImportJobPayload(
    Guid PackageJobId,
    CatalogDuplicateStrategy Strategy,
    CatalogPackageOptions Options,
    string? PackagePassword);
