using HAMBOX.Modules.Suppliers.Domain.Suppliers;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// The one "is this mapped supplier product currently offered, and is that answer still fresh" decision,
/// shared by <see cref="FulfillmentRouter"/> (checkout-time/storefront readiness) and
/// <c>SupplierRoutingEngine</c> (post-payment cheapest-supplier selection) so the two can never disagree
/// about what "available" means for the same mapping. Never calls a provider live — reads only the
/// persisted <see cref="SupplierProductAvailability"/> cache, exactly as both callers already required.
/// </summary>
public static class SupplierAvailabilityFreshness
{
    public static bool IsAvailableAndFresh(SupplierProductAvailability? availability, TimeSpan staleAfter, DateTimeOffset utcNow)
    {
        if (availability is not { AvailabilityState: SupplierAvailabilityState.Available, LastCheckedAtUtc: DateTimeOffset checkedAtUtc })
        {
            return false;
        }

        return utcNow - checkedAtUtc <= staleAfter;
    }
}
