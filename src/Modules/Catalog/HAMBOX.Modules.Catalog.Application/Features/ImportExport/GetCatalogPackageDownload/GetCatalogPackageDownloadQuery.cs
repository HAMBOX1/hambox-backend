using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageDownload;

public sealed record CatalogPackageDownloadDto(string FileName, string ContentType, byte[] Content);

/// <summary>Downloads a completed job's result file — the generated export package, or an import's error/summary report.</summary>
public sealed record GetCatalogPackageDownloadQuery(Guid JobId) : IRequest<Result<CatalogPackageDownloadDto>>;
