using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageJob;

public sealed record GetCatalogPackageJobQuery(Guid JobId) : IRequest<Result<CatalogPackageJobDto>>;
