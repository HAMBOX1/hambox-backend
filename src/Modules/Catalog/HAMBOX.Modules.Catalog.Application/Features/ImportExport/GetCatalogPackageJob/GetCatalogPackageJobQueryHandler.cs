using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageJob;

internal sealed class GetCatalogPackageJobQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetCatalogPackageJobQuery, Result<CatalogPackageJobDto>>
{
    public async Task<Result<CatalogPackageJobDto>> Handle(GetCatalogPackageJobQuery request, CancellationToken cancellationToken)
    {
        var job = await dbContext.CatalogPackageJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken);

        return job is null
            ? Result.Failure<CatalogPackageJobDto>(CatalogErrors.PackageJobNotFound)
            : Result.Success(CatalogPackageJobMapper.ToDto(job));
    }
}
