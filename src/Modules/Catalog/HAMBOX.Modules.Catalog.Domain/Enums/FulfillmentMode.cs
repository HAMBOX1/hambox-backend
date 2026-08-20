namespace HAMBOX.Modules.Catalog.Domain.Enums;

/// <summary>
/// Controls how a <c>ProductVariant</c>'s paid order-item shortfall is sourced. Purely a Catalog
/// concept — nothing here names a supplier or provider; the actual routing decision this drives lives
/// in Commerce's <c>IFulfillmentRouter</c>, which resolves suppliers generically via
/// <c>ISupplierProviderRegistry</c>. <see cref="ManualOnly"/> is the default for every existing and
/// newly-created variant, so no product starts spending supplier money without an explicit admin choice.
/// </summary>
public enum FulfillmentMode
{
    /// <summary>Manual inventory only. Automated suppliers are never contacted for this variant.</summary>
    ManualOnly = 0,

    /// <summary>
    /// Consume available manual inventory first. If a quantity shortfall remains, the configured
    /// automated-supplier chain is asked for exactly that remaining quantity.
    /// </summary>
    ManualFirst = 1,

    /// <summary>
    /// Attempt the configured automated-supplier chain first, for the full requested quantity, without
    /// touching manual inventory. If a supplier attempt reaches a DEFINITIVE terminal outcome that still
    /// leaves a shortfall, manual inventory may cover the remainder. An ambiguous/unresolved supplier
    /// outcome never triggers a manual fallback — it must be reconciled first.
    /// </summary>
    SupplierFirst = 2,

    /// <summary>Automated supplier only. Manual inventory is never reserved, assigned, or used as a fallback.</summary>
    SupplierOnly = 3,
}
