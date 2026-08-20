namespace HAMBOX.Modules.Commerce.Domain.Enums;

/// <summary>
/// Lifecycle of an asynchronous, redirect-based payment attempt (e.g. DOT). Distinct from
/// <see cref="PaymentStatus"/>, which tracks the owning <c>Order</c>'s settled payment state —
/// this tracks the in-flight attempt itself, including the transient <see cref="Verifying"/>
/// state used to serialize concurrent finalization attempts (browser callback, provider webhook,
/// and background reconciliation can all race to finalize the same attempt).
/// </summary>
public enum PaymentAttemptStatus
{
    Pending = 0,
    Verifying = 1,
    Succeeded = 2,
    Failed = 3,
    Expired = 4,
}
