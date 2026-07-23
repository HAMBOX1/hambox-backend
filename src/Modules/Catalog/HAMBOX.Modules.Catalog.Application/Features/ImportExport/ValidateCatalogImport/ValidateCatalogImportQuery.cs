using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ValidateCatalogImport;

public sealed record ValidateCatalogImportQuery(
    Guid UploadId, string? PackagePassword) : IRequest<Result<CatalogImportValidationReport>>;
