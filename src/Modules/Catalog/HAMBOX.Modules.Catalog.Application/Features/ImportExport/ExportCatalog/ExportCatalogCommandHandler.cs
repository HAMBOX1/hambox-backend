using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExportCatalog;

internal sealed class ExportCatalogCommandHandler(
    ICatalogDbContext dbContext,
    IBackgroundJobScheduler jobScheduler,
    ICatalogPackageSecretProtector secretProtector,
    ICurrentUserPermissions permissions)
    : IRequestHandler<ExportCatalogCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ExportCatalogCommand request, CancellationToken cancellationToken)
    {
        // A package can carry plaintext redemption codes and supplier buying prices. The Export permission alone must
        // not be a way around the dedicated reveal / cost permissions those data are otherwise behind.
        if (request.Options.IncludeDigitalCodes && !permissions.Has(CatalogPermissions.RevealCodes))
        {
            return Result.Failure<Guid>(CatalogErrors.ExportCodesNotPermitted);
        }

        if (request.Options.IncludeSuppliers && !permissions.Has(CatalogPermissions.ViewCosts))
        {
            return Result.Failure<Guid>(CatalogErrors.ExportCostsNotPermitted);
        }

        IReadOnlyList<Guid> productIds;

        if (request.Scope.ExportEntireCatalog)
        {
            productIds = await dbContext.Products.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);
        }
        else
        {
            var selection = new BulkProductSelection(
                request.Scope.ProductIds,
                request.Scope.SearchTerm,
                request.Scope.Status,
                request.Scope.CategoryId,
                request.Scope.SelectAllMatching);

            var resolved = await BulkProductSelectionResolver.ResolveAsync(dbContext, selection, cancellationToken);
            if (resolved.IsFailure)
            {
                return Result.Failure<Guid>(resolved.Error);
            }

            productIds = resolved.Value;
        }

        if (productIds.Count == 0)
        {
            return Result.Failure<Guid>(CatalogErrors.ExportScopeEmpty);
        }

        var packageJob = CatalogPackageJob.CreateExportRequest(request.Format);
        dbContext.CatalogPackageJobs.Add(packageJob);
        await dbContext.SaveChangesAsync(cancellationToken);

        var protectedPassword = string.IsNullOrEmpty(request.PackagePassword)
            ? request.PackagePassword
            : secretProtector.Protect(request.PackagePassword);

        var payload = new ExportCatalogJobPayload(
            packageJob.Id, productIds, request.Format, request.Options, request.EncryptCodes,
            request.PasswordProtectPackage, protectedPassword);

        var backgroundJobId = await jobScheduler.EnqueueAsync(
            CatalogJobTypes.ExportPackage,
            payload,
            new BackgroundJobOptions(
                Queue: BackgroundJobQueues.Reports,
                RelatedEntityType: nameof(CatalogPackageJob),
                RelatedEntityId: packageJob.Id.ToString()),
            cancellationToken);

        packageJob.AttachBackgroundJob(backgroundJobId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(packageJob.Id);
    }
}
