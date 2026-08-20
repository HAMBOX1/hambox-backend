using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// A no-supplier-available stand-in for tests that exercise <c>OrderFulfillmentService</c>'s existing
/// manual-inventory path only and never expect an automated supplier to be involved — every method
/// reports "nothing to do" rather than throwing, matching what the real service does when a product
/// simply has no supplier mapping.
/// </summary>
internal sealed class NullSupplierFulfillmentService : ISupplierFulfillmentService
{
    public int RequestFulfillmentCallCount { get; private set; }

    public Task<Result<SupplierFulfillmentOutcome>> RequestFulfillmentAsync(SupplierFulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        RequestFulfillmentCallCount++;
        return Task.FromResult(Result.Failure<SupplierFulfillmentOutcome>(SupplierFulfillmentTestErrors.NoSupplierConfigured));
    }

    public Task<Result<SupplierFulfillmentOutcome>> ProcessAsync(Guid fulfillmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<SupplierFulfillmentOutcome>(SupplierFulfillmentTestErrors.NoSupplierConfigured));

    public Task<Result<SupplierFulfillmentOutcome>> ReconcileAsync(Guid fulfillmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<SupplierFulfillmentOutcome>(SupplierFulfillmentTestErrors.NoSupplierConfigured));

    public Task<SupplierFulfillmentSweepResult> ProcessDueFulfillmentsAsync(int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierFulfillmentSweepResult(0, 0, 0, 0, 0, 0));

    public Task<bool> IsScopeExhaustedAsync(
        Guid orderId, Guid orderItemId, Guid supplierId, Guid supplierProductMappingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

file static class SupplierFulfillmentTestErrors
{
    public static readonly HAMBOX.SharedKernel.Errors.Error NoSupplierConfigured = new(
        "Test.NoSupplierConfigured", "No supplier configured in this test double.");
}
