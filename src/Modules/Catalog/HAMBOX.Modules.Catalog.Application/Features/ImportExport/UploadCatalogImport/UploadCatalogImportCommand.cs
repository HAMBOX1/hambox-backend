using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.UploadCatalogImport;

public sealed record UploadCatalogImportCommand(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    CatalogImportEntityType EntityType) : IRequest<Result<CatalogPackageJobDto>>;
