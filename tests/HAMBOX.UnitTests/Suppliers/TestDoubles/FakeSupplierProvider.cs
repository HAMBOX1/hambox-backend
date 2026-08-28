using HAMBOX.Modules.Suppliers.Application.Abstractions;

namespace HAMBOX.UnitTests.Suppliers.TestDoubles;

/// <summary>
/// Deterministic, fully-configurable <see cref="ISupplierProvider"/> test double. Every scenario
/// (success, partial success, explicit failure, ambiguous/timeout, reconciliation outcomes, provider
/// unavailability) is driven by a delegate the test sets explicitly — nothing here is random. Every
/// call is recorded so tests can assert exactly how many purchase/status calls happened and with what
/// arguments, including detecting a duplicated <c>ReferenceId</c> (the HamboxReferenceId).
/// </summary>
internal sealed class FakeSupplierProvider(string providerType = "Fake") : ISupplierProvider
{
    private readonly List<PurchaseCallRecord> _purchaseCalls = [];
    private readonly List<StatusCallRecord> _statusCalls = [];
    private readonly HashSet<string> _seenPurchaseReferenceIds = [];

    public string ProviderType => providerType;

    /// <summary>Settable so routing-engine tests can simulate a provider that caps quantity (e.g. GlobeTopper's real 1).</summary>
    public int? MaxQuantityPerPurchase { get; set; }

    public IReadOnlyList<PurchaseCallRecord> PurchaseCalls => _purchaseCalls;

    public IReadOnlyList<StatusCallRecord> StatusCalls => _statusCalls;

    /// <summary>Number of <see cref="PurchaseAsync"/> calls that reused a <c>ReferenceId</c> already seen by this instance.</summary>
    public int DuplicateReferenceIdCount { get; private set; }

    public bool ConnectionTestSucceeds { get; set; } = true;

    /// <summary>When set, overrides the default (full-success) <see cref="PurchaseAsync"/> response.</summary>
    public Func<SupplierPurchaseRequest, SupplierProviderContext, SupplierPurchaseResult>? PurchaseResponder { get; set; }

    /// <summary>When set, <see cref="PurchaseAsync"/> throws this instead of returning — the "ambiguous/timeout" scenario.</summary>
    public Func<SupplierPurchaseRequest, SupplierProviderContext, Exception>? PurchaseThrows { get; set; }

    /// <summary>When set, overrides the default <see cref="GetOrderStatusAsync"/> response.</summary>
    public Func<SupplierOrderStatusQuery, SupplierProviderContext, SupplierOrderStatusResult>? StatusResponder { get; set; }

    /// <summary>When set, <see cref="GetOrderStatusAsync"/> throws instead of returning.</summary>
    public Func<SupplierOrderStatusQuery, SupplierProviderContext, Exception>? StatusThrows { get; set; }

    public Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierConnectionTestResult(ConnectionTestSucceeds, ConnectionTestSucceeds ? "ok" : "fake connection failure"));

    public Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCredentialValidationResult(true, null));

    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "not supported by fake"));

    /// <summary>When set, overrides the default (unsupported) <see cref="SearchCatalogAsync"/> response.</summary>
    public SupplierCatalogSearchResult? CatalogResponse { get; set; }

    public Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(CatalogResponse ?? new SupplierCatalogSearchResult(false, [], "not supported by fake"));

    /// <summary>When set, overrides the default (unsupported) <see cref="GetAvailabilityAsync"/> response.</summary>
    public SupplierAvailabilityResult? AvailabilityResponse { get; set; }

    /// <summary>When set, <see cref="GetAvailabilityAsync"/> throws instead of returning.</summary>
    public Func<SupplierAvailabilityQuery, SupplierProviderContext, Exception>? AvailabilityThrows { get; set; }

    public IReadOnlyList<SupplierAvailabilityQuery> AvailabilityCalls => _availabilityCalls;

    private readonly List<SupplierAvailabilityQuery> _availabilityCalls = [];

    public Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        _availabilityCalls.Add(query);

        if (AvailabilityThrows is not null)
        {
            throw AvailabilityThrows(query, context);
        }

        return Task.FromResult(AvailabilityResponse ?? new SupplierAvailabilityResult(false, [], "not supported by fake"));
    }

    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "not supported by fake"));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "not supported by fake"));

    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "not supported by fake"));

    public Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        if (request.ReferenceId is not null && !_seenPurchaseReferenceIds.Add(request.ReferenceId))
        {
            DuplicateReferenceIdCount++;
        }

        _purchaseCalls.Add(new PurchaseCallRecord(context.SupplierId, request.ExternalProductId, request.Quantity, request.ReferenceId));

        if (PurchaseThrows is not null)
        {
            throw PurchaseThrows(request, context);
        }

        if (PurchaseResponder is not null)
        {
            return Task.FromResult(PurchaseResponder(request, context));
        }

        // Default scenario: immediate, full, synchronous success — one fake code per requested unit.
        var codes = Enumerable.Range(1, request.Quantity)
            .Select(i => $"FAKE-CODE-{request.ReferenceId}-{i}")
            .ToArray();
        return Task.FromResult(new SupplierPurchaseResult(true, $"PROV-{request.ReferenceId}", codes, null, null));
    }

    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "not supported by fake"));

    public Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        _statusCalls.Add(new StatusCallRecord(context.SupplierId, query.HamboxReferenceId, query.ProviderOrderId));

        if (StatusThrows is not null)
        {
            throw StatusThrows(query, context);
        }

        if (StatusResponder is not null)
        {
            return Task.FromResult(StatusResponder(query, context));
        }

        return Task.FromResult(new SupplierOrderStatusResult(SupplierProviderOrderStatus.Unknown, query.ProviderOrderId, null, null, "no configured response"));
    }

    public sealed record PurchaseCallRecord(Guid SupplierId, string ExternalProductId, int Quantity, string? ReferenceId);

    public sealed record StatusCallRecord(Guid SupplierId, Guid HamboxReferenceId, string? ProviderOrderId);
}
