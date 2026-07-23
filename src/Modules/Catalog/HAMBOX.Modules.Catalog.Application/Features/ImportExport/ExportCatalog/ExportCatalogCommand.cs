using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExportCatalog;

public sealed record ExportCatalogCommand(
    CatalogExportScope Scope,
    CatalogPackageFormat Format,
    CatalogPackageOptions Options,
    bool EncryptCodes,
    bool PasswordProtectPackage,
    string? PackagePassword) : IRequest<Result<Guid>>;
