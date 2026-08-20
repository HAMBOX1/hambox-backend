namespace HAMBOX.Modules.Suppliers.Domain.Fulfillments;

/// <summary>
/// Lifecycle of one automated-supplier purchase attempt covering a shortfall quantity for a single
/// order item. Transitions are deliberately narrow: <see cref="Submitting"/> is the only state from
/// which the external purchase call is made, and it is entered exactly once per successful
/// <see cref="SupplierFulfillment.Claim"/> — see that method's remarks for the concurrency contract.
/// </summary>
public enum SupplierFulfillmentStatus
{
    /// <summary>Created, not yet claimed by a worker.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker; the external purchase call is about to be made or is in flight.</summary>
    Submitting = 1,

    /// <summary>The provider confirmed it accepted the request; delivery outcome not yet known.</summary>
    Submitted = 2,

    /// <summary>
    /// The purchase call's outcome is ambiguous (timeout/network failure with no definite HTTP
    /// response). Must be resolved via the provider's status-lookup API before any further action —
    /// never by blindly re-submitting the purchase.
    /// </summary>
    Unknown = 3,

    /// <summary>Terminal: the full requested quantity was delivered.</summary>
    Succeeded = 4,

    /// <summary>Terminal for this attempt: some, but not all, of the requested quantity was delivered.</summary>
    PartialFailed = 5,

    /// <summary>Terminal: nothing was delivered.</summary>
    Failed = 6,
}

/// <summary>
/// Safe, provider-agnostic failure classification. Only documented provider behavior may map into a
/// specific category here — an unrecognized or undocumented provider response must map to
/// <see cref="UnknownProviderState"/>, never be guessed into a more specific one.
/// </summary>
public enum SupplierFulfillmentFailureCategory
{
    ProviderUnavailable = 0,
    ProviderTimeout = 1,
    AuthenticationFailed = 2,
    InsufficientSupplierBalance = 3,
    ProductUnavailable = 4,
    InvalidProduct = 5,
    InvalidDenomination = 6,
    DuplicateRequest = 7,
    InvalidConfiguration = 8,
    UnknownProviderState = 9,
}
