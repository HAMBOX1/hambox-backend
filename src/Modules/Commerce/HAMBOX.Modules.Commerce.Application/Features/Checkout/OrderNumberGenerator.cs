namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

/// <summary>
/// Single source for order-number generation, shared by every checkout path (standard, DOT, DOT
/// Fawry, membership) so the admin-configurable <c>commerce.invoicePrefix</c> Platform Setting only
/// needs wiring in one place instead of four identical copies.
/// </summary>
internal static class OrderNumberGenerator
{
    public static string Generate(string prefix)
    {
        var resolvedPrefix = string.IsNullOrWhiteSpace(prefix) ? "ORD-" : prefix;
        return $"{resolvedPrefix}{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}
