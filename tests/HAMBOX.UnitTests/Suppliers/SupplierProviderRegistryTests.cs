using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;

namespace HAMBOX.UnitTests.Suppliers;

public sealed class SupplierProviderRegistryTests
{
    [Fact]
    public void Resolve_ReturnsRegisteredProvider_ByProviderTypeKey()
    {
        var manual = new ManualSupplierProvider();
        var registry = new SupplierProviderRegistry([manual]);

        var result = registry.Resolve("Manual");

        Assert.True(result.IsSuccess);
        Assert.Same(manual, result.Value);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var registry = new SupplierProviderRegistry([new ManualSupplierProvider()]);

        var result = registry.Resolve("manual");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_ReturnsFailure_ForUnregisteredProviderType()
    {
        var registry = new SupplierProviderRegistry([new ManualSupplierProvider()]);

        var result = registry.Resolve("Bamboo");

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.ProviderTypeNotRegistered", result.Error.Code);
    }

    [Fact]
    public void GetRegisteredProviderTypes_ReflectsWhateverIsRegistered_WithoutAnyCodeBranching()
    {
        // Adding a new provider is purely "register another ISupplierProvider" — this asserts the
        // registry has no hardcoded knowledge of "Manual" or any other specific provider type.
        var fakeProvider = new FakeSupplierProvider("Future");
        var registry = new SupplierProviderRegistry([new ManualSupplierProvider(), fakeProvider]);

        var types = registry.GetRegisteredProviderTypes();

        Assert.Contains("Manual", types);
        Assert.Contains("Future", types);
        Assert.True(registry.Resolve("Future").IsSuccess);
    }

    private sealed class FakeSupplierProvider(string providerType) : ISupplierProvider
    {
        public string ProviderType => providerType;

        public Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierConnectionTestResult(true, "ok"));

        public Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierCredentialValidationResult(true, null));

        public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierProductSyncResult(true, 0, null));

        public Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierAvailabilityResult(true, [], null));

        public Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierCatalogSearchResult(true, [], null));

        public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierInventorySyncResult(true, 0, null));

        public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierPriceSyncResult(true, 0, null));

        public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierReservationResult(true, null, null));

        public Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierPurchaseResult(true, null, null, null, null));

        public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierCancellationResult(true, null));

        public Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupplierOrderStatusResult(SupplierProviderOrderStatus.Unknown, null, null, null, null));
    }
}
