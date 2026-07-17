using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class SupplierHealthCheckJobHandler(
    IBackgroundJobSerializer serializer,
    ICatalogDbContext catalogDb,
    ICommerceDbContext commerceDb) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.SupplierHealthCheck;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var inactive = await catalogDb.InventorySuppliers.AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.Status != SupplierStatus.Active, cancellationToken);

        if (inactive > 0)
        {
            await OperationalAlertUpsert.UpsertAsync(
                commerceDb,
                "SUPPLIER_INACTIVE",
                "Inactive suppliers",
                $"{inactive} supplier(s) are not Active.",
                OperationalAlertSeverity.Info,
                cancellationToken);
            await commerceDb.SaveChangesAsync(cancellationToken);
        }
    }
}
