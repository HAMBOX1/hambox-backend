using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Fulfillments;

/// <summary>
/// A single record of what the cheapest-eligible-supplier routing engine decided for one order item's
/// shortfall — which supplier it picked and why, every candidate it considered, and whether it had to
/// fall back after a definitive failure. Purely descriptive/append-only, exactly like
/// <c>PlatformSettingsAuditLog</c>/<c>InventoryAuditLog</c>'s per-module dedicated-audit-entity
/// convention (see CLAUDE.md §3) — this is NOT a second fulfillment state machine; <see cref="SupplierFulfillment"/>
/// remains the only source of truth for what actually happened with a provider. One row is written per
/// routing decision (i.e. once per shortfall line processed by <c>OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync</c>),
/// after the failover loop concludes, so it can describe the final outcome alongside every candidate
/// considered.
/// </summary>
/// <remarks>
/// <see cref="CandidatesJson"/> is a safe, admin-facing summary only — supplier name, provider type,
/// normalized cost, currency, and eligible/rejected + reason for every mapping considered. It must never
/// contain a credential, a raw provider response, or a delivered redemption code; see
/// <c>SupplierRoutingEngine</c> (Commerce.Infrastructure), the only writer of this shape, for the exact
/// fields serialized.
/// </remarks>
public sealed class SupplierRoutingAuditLog : AggregateRoot, IAuditable
{
    private SupplierRoutingAuditLog()
    {
    }

    private SupplierRoutingAuditLog(
        Guid id,
        Guid orderId,
        Guid orderItemId,
        Guid? selectedSupplierId,
        Guid? selectedSupplierProductMappingId,
        decimal? selectedCostInBaseCurrency,
        string baseCurrency,
        bool fallbackOccurred,
        string candidatesJson)
        : base(id)
    {
        OrderId = orderId;
        OrderItemId = orderItemId;
        SelectedSupplierId = selectedSupplierId;
        SelectedSupplierProductMappingId = selectedSupplierProductMappingId;
        SelectedCostInBaseCurrency = selectedCostInBaseCurrency;
        BaseCurrency = baseCurrency;
        FallbackOccurred = fallbackOccurred;
        CandidatesJson = candidatesJson;
    }

    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }

    /// <summary>Null when no eligible candidate existed at all (every mapping was rejected) — a real, distinct outcome from "selected but every attempt failed."</summary>
    public Guid? SelectedSupplierId { get; private set; }
    public Guid? SelectedSupplierProductMappingId { get; private set; }

    /// <summary>The normalized (base-currency) acquisition cost of the selected candidate at the moment routing ran — never the customer-facing selling price.</summary>
    public decimal? SelectedCostInBaseCurrency { get; private set; }
    public string BaseCurrency { get; private set; } = "USD";

    /// <summary>True when the selected supplier was not the first (cheapest) candidate tried — i.e. one or more cheaper candidates definitively failed first.</summary>
    public bool FallbackOccurred { get; private set; }

    /// <summary>Safe summary of every candidate considered (eligible or rejected, with cost/reason) — see remarks. Never credentials, never a raw provider response.</summary>
    public string CandidatesJson { get; private set; } = "[]";

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static SupplierRoutingAuditLog Create(
        Guid orderId,
        Guid orderItemId,
        Guid? selectedSupplierId,
        Guid? selectedSupplierProductMappingId,
        decimal? selectedCostInBaseCurrency,
        string baseCurrency,
        bool fallbackOccurred,
        string candidatesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatesJson);

        return new SupplierRoutingAuditLog(
            Guid.NewGuid(), orderId, orderItemId, selectedSupplierId, selectedSupplierProductMappingId,
            selectedCostInBaseCurrency, baseCurrency.Trim().ToUpperInvariant(), fallbackOccurred, candidatesJson);
    }
}
