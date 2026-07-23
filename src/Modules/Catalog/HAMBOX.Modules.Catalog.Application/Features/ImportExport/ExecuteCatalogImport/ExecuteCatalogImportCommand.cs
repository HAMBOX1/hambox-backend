using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExecuteCatalogImport;

public sealed record ExecuteCatalogImportCommand(
    Guid UploadId,
    CatalogDuplicateStrategy Strategy,
    CatalogPackageOptions Options,
    string? PackagePassword) : IRequest<Result<Guid>>;
