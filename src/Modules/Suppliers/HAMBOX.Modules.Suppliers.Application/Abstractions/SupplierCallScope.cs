namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// Ambient description of the fulfillment attempt an outbound supplier call belongs to, so the HTTP-level call log
/// (which only sees a request and a response) can be tied back to an order, an attempt and HAMBOX's own reference id.
/// Flows with the async call chain; nested scopes restore the outer one when disposed.
/// </summary>
public static class SupplierCallScope
{
    private static readonly AsyncLocal<SupplierCallInfo?> CurrentInfo = new();

    public static SupplierCallInfo? Current => CurrentInfo.Value;

    public static IDisposable Begin(SupplierCallInfo info)
    {
        var previous = CurrentInfo.Value;
        CurrentInfo.Value = info;
        return new Restore(previous);
    }

    private sealed class Restore(SupplierCallInfo? previous) : IDisposable
    {
        public void Dispose() => CurrentInfo.Value = previous;
    }
}

public sealed record SupplierCallInfo(
    Guid? FulfillmentId,
    Guid? HamboxReferenceId,
    Guid? SupplierId,
    Guid? OrderId,
    int? Attempt);
