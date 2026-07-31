using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageJobs;

public sealed record GetCatalogPackageJobsQuery(
    CatalogPackageDirection? Direction,
    CatalogPackageJobStatus? Status,
    string? Search,
    int Page,
    int PageSize,
    string? Sort) : IRequest<Result<PagedResult<CatalogPackageJobDto>>>;
