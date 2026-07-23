using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageDownload;

internal sealed class GetCatalogPackageDownloadQueryHandler(ICatalogDbContext dbContext, IFileStorage fileStorage)
    : IRequestHandler<GetCatalogPackageDownloadQuery, Result<CatalogPackageDownloadDto>>
{
    public async Task<Result<CatalogPackageDownloadDto>> Handle(
        GetCatalogPackageDownloadQuery request, CancellationToken cancellationToken)
    {
        var job = await dbContext.CatalogPackageJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken);

        if (job is null || job.Status != CatalogPackageJobStatus.Completed || string.IsNullOrEmpty(job.ResultStorageKey))
        {
            return Result.Failure<CatalogPackageDownloadDto>(CatalogErrors.PackageJobNotFound);
        }

        await using var stream = await fileStorage.OpenReadAsync(job.ResultStorageKey, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var fileName = job.ResultFileName ?? "export.hambox";
        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".hambox" => "application/zip",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream",
        };

        return Result.Success(new CatalogPackageDownloadDto(fileName, contentType, buffer.ToArray()));
    }
}
