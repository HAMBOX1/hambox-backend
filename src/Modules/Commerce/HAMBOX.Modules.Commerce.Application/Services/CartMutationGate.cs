namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Serializes cart writes for one owner (user id or guest session). A double-click on "Add to cart" sent two requests
/// that both saw no item and both inserted it; the loser hit the unique cart-item index and answered 500. Striped
/// (fixed 64 locks, no growth) because the API runs as a single instance.
/// ponytail: in-process only — a multi-instance deployment needs a database-level upsert instead.
/// </summary>
internal static class CartMutationGate
{
    private static readonly SemaphoreSlim[] Stripes = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public static async Task<T> RunAsync<T>(string ownerKey, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var stripe = Stripes[(uint)StringComparer.Ordinal.GetHashCode(ownerKey) % Stripes.Length];
        await stripe.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            stripe.Release();
        }
    }
}
