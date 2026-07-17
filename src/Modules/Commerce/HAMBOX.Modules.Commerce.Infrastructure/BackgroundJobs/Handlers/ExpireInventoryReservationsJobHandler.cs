using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class ExpireInventoryReservationsJobHandler(IBackgroundJobSerializer serializer, IInventoryEngine inventory)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ExpireInventoryReservations;

    public override Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken) =>
        inventory.ExpireStaleReservationsAsync(cancellationToken);
}
