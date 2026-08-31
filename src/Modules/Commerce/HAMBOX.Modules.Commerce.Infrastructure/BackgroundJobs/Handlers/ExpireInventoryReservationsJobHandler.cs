using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Only this standalone recurring sweep is gated by <c>inventory.automaticReleaseEnabled</c> — the
/// inline defensive release calls inside checkout/reservation flows (InventoryEngine, CheckoutCommandHandler)
/// stay unconditional, since those exist to prevent overselling, not as an admin-toggleable convenience.
/// </summary>
internal sealed class ExpireInventoryReservationsJobHandler(
    IBackgroundJobSerializer serializer,
    IInventoryEngine inventory,
    IPlatformSettingsProvider platformSettings)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ExpireInventoryReservations;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var inventorySettings = await platformSettings.GetInventoryAsync(cancellationToken);
        if (!inventorySettings.AutomaticReleaseEnabled)
        {
            return;
        }

        await inventory.ExpireStaleReservationsAsync(cancellationToken);
    }
}
