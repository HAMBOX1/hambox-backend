using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExecuteCatalogImport;

internal sealed class ExecuteCatalogImportCommandHandler(
    ICatalogDbContext dbContext,
    IBackgroundJobScheduler jobScheduler,
    ICatalogPackageSecretProtector secretProtector)
    : IRequestHandler<ExecuteCatalogImportCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ExecuteCatalogImportCommand request, CancellationToken cancellationToken)
    {
        var job = await dbContext.CatalogPackageJobs
            .FirstOrDefaultAsync(j => j.Id == request.UploadId, cancellationToken);

        if (job is null || job.Direction != CatalogPackageDirection.Import)
        {
            return Result.Failure<Guid>(CatalogErrors.PackageJobNotFound);
        }

        if (job.Status is CatalogPackageJobStatus.Completed or CatalogPackageJobStatus.Queued or CatalogPackageJobStatus.Processing)
        {
            return Result.Failure<Guid>(CatalogErrors.PackageAlreadyExecuted);
        }

        var protectedPassword = string.IsNullOrEmpty(request.PackagePassword)
            ? request.PackagePassword
            : secretProtector.Protect(request.PackagePassword);

        var payload = new ExecuteCatalogImportJobPayload(
            job.Id, request.Strategy, request.Options, protectedPassword,
            request.SkuStrategy, request.Corrections, request.RowOverrides);

        var backgroundJobId = await jobScheduler.EnqueueAsync(
            CatalogJobTypes.ImportPackage,
            payload,
            new BackgroundJobOptions(
                Queue: BackgroundJobQueues.Reports,
                RelatedEntityType: nameof(CatalogPackageJob),
                RelatedEntityId: job.Id.ToString()),
            cancellationToken);

        job.AttachBackgroundJob(backgroundJobId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(job.Id);
    }
}
