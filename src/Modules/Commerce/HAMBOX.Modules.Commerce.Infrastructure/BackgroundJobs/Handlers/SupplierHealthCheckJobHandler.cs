using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Checks <c>Catalog.InventorySupplier</c> — the manual-inventory vendor-contact list — not the newer
/// <c>Suppliers.Supplier</c> registry (schema <c>suppliers</c>) used for automated integrations. The
/// job's name predates that module; it was not renamed to avoid implying a monitoring capability for
/// automated suppliers that doesn't exist yet. Disabling/enabling a <c>Suppliers.Supplier</c> row has
/// no effect on this check.
/// </summary>
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
                "INVENTORY_SUPPLIER_INACTIVE",
                "Inactive inventory vendors",
                $"{inactive} inventory vendor(s) are not Active.",
                OperationalAlertSeverity.Info,
                cancellationToken);
            await commerceDb.SaveChangesAsync(cancellationToken);
        }
    }
}
