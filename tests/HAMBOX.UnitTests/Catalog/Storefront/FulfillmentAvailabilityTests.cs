using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.UnitTests.Catalog.Storefront;

/// <summary>
/// The single shared purchasability rule both <c>CartLineValidator</c> (checkout) and Catalog's
/// storefront configuration builder (product card / PDP) now call — this is the exact truth table
/// from <c>CartLineValidator</c>'s original inline switch (see <c>FulfillmentRoutingTests</c> for the
/// checkout-side integration coverage), verified here as a pure decision table so both callers are
/// provably answering the same question the same way.
/// </summary>
public sealed class FulfillmentAvailabilityTests
{
    [Theory]
    [InlineData(FulfillmentMode.ManualOnly, false, false, false)] // manual 0 -> unavailable
    [InlineData(FulfillmentMode.ManualOnly, true, false, true)] // manual >0 -> available
    [InlineData(FulfillmentMode.ManualOnly, false, true, false)] // supplier mapping existing must NOT rescue ManualOnly
    public void ManualOnly_IgnoresSupplierEntirely(FulfillmentMode mode, bool manualSufficient, bool supplierReady, bool expected) =>
        Assert.Equal(expected, FulfillmentAvailability.IsAvailable(mode, manualSufficient, supplierReady));

    [Theory]
    [InlineData(false, true, true)] // manual 0 + supplier ready -> available
    [InlineData(false, false, false)] // manual 0 + supplier not ready -> unavailable
    [InlineData(true, false, true)] // manual >0 -> available regardless of supplier
    [InlineData(true, true, true)]
    public void ManualFirst_ManualOrSupplier(bool manualSufficient, bool supplierReady, bool expected) =>
        Assert.Equal(expected, FulfillmentAvailability.IsAvailable(FulfillmentMode.ManualFirst, manualSufficient, supplierReady));

    [Theory]
    [InlineData(false, true, true)] // supplier ready + manual 0 -> available
    [InlineData(false, false, false)] // supplier not ready + manual 0 -> unavailable
    [InlineData(true, false, false)] // manual stock existing must NOT substitute for a not-ready supplier
    [InlineData(true, true, true)]
    public void SupplierFirst_SupplierReadinessAloneDecides(bool manualSufficient, bool supplierReady, bool expected) =>
        Assert.Equal(expected, FulfillmentAvailability.IsAvailable(FulfillmentMode.SupplierFirst, manualSufficient, supplierReady));

    [Theory]
    [InlineData(false, true, true)] // supplier ready + manual 0 -> available
    [InlineData(true, false, false)] // supplier not ready + manual >0 -> unavailable (manual must never rescue SupplierOnly)
    [InlineData(false, false, false)] // supplier not ready + manual 0 -> unavailable
    [InlineData(true, true, true)]
    public void SupplierOnly_SupplierReadinessAloneDecides(bool manualSufficient, bool supplierReady, bool expected) =>
        Assert.Equal(expected, FulfillmentAvailability.IsAvailable(FulfillmentMode.SupplierOnly, manualSufficient, supplierReady));

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SupplierFirst_And_SupplierOnly_AlwaysAgree(bool manualSufficient, bool supplierReady)
    {
        // Both modes route exclusively through supplier readiness once manual-only is ruled out —
        // this pins that CartLineValidator's shared case label for the two never silently diverges.
        Assert.Equal(
            FulfillmentAvailability.IsAvailable(FulfillmentMode.SupplierFirst, manualSufficient, supplierReady),
            FulfillmentAvailability.IsAvailable(FulfillmentMode.SupplierOnly, manualSufficient, supplierReady));
    }
}
