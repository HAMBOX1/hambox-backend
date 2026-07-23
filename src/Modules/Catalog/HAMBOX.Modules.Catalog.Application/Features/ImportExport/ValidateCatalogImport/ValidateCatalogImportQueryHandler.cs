using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ValidateCatalogImport;

internal sealed class ValidateCatalogImportQueryHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage,
    ICatalogImportParser parser)
    : IRequestHandler<ValidateCatalogImportQuery, Result<CatalogImportValidationReport>>
{
    public async Task<Result<CatalogImportValidationReport>> Handle(
        ValidateCatalogImportQuery request, CancellationToken cancellationToken)
    {
        var job = await dbContext.CatalogPackageJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.UploadId, cancellationToken);

        if (job is null || job.Direction != CatalogPackageDirection.Import)
        {
            return Result.Failure<CatalogImportValidationReport>(CatalogErrors.PackageJobNotFound);
        }

        if (job.Status == CatalogPackageJobStatus.Completed)
        {
            return Result.Failure<CatalogImportValidationReport>(CatalogErrors.PackageAlreadyExecuted);
        }

        ParsedCatalogPackage package;
        try
        {
            await using var stream = await fileStorage.OpenReadAsync(job.StorageKey, cancellationToken);
            package = await parser.ParseAsync(stream, job.Format, job.EntityType, request.PackagePassword, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure<CatalogImportValidationReport>(
                string.IsNullOrEmpty(request.PackagePassword)
                    ? CatalogErrors.PackagePasswordRequired
                    : CatalogErrors.InvalidPackagePassword);
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException)
        {
            return Result.Failure<CatalogImportValidationReport>(CatalogErrors.PackageParsingFailed);
        }

        var plan = await CatalogImportMatcher.BuildPlanAsync(package, dbContext, cancellationToken);
        var report = CatalogImportMatcher.ToReport(job.Id, job.Format, job.EntityType, plan);

        return Result.Success(report);
    }
}
