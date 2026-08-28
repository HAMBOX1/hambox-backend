using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// A no-eligible-candidate stand-in for tests that exercise <c>OrderFulfillmentService</c>'s manual-inventory
/// or payment-webhook paths only, or <c>CartLineValidator</c>'s non-supplier lines only, and never expect
/// supplier-derived pricing to be involved — mirrors <see cref="NullSupplierRoutingEngine"/>'s identical
/// "nothing to do" convention, one layer up.
/// </summary>
internal sealed class NullSupplierPricingEngine : ISupplierPricingEngine
{
    public Task<SupplierPricingResult> ResolveAsync(SupplierRoutingRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPricingResult([], [], "USD"));
}
