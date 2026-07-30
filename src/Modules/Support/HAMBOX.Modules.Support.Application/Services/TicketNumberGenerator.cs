namespace HAMBOX.Modules.Support.Application.Services;

internal static class TicketNumberGenerator
{
    // Mirrors Commerce's Order.OrderNumber generation scheme (CheckoutCommandHandler).
    public static string Generate() =>
        $"TCK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
