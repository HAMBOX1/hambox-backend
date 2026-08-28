using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// A no-eligible-candidate stand-in for tests that exercise <c>OrderFulfillmentService</c>'s manual-inventory
/// or payment-webhook paths only and never expect automated-supplier routing to be involved — mirrors
/// <see cref="NullSupplierFulfillmentService"/>'s identical "nothing to do" convention.
/// </summary>
internal sealed class NullSupplierRoutingEngine : ISupplierRoutingEngine
{
    public Task<SupplierRoutingResult> ResolveAsync(SupplierRoutingRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierRoutingResult([], [], "USD"));
}
