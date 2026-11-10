using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Points;

/// <summary>
/// Awards and reverses purchase points against the append-only <see cref="PointsTransaction"/> ledger.
/// Both operations are idempotent (the ledger's unique key rejects a second row for the same effect) and are
/// deliberately best-effort: they run inside order completion / refund flows, so a failure here is logged and
/// swallowed rather than allowed to roll back a payment confirmation. Amounts are USD — the canonical stored
/// price currency — so display currency never affects points.
/// </summary>
public sealed class PurchasePointsService(
    ICommerceDbContext db,
    IPlatformSettingsProvider platformSettings,
    ILogger<PurchasePointsService> logger)
{
    /// <summary>
    /// Credits points for a paid, completed order at the rate configured right now. A no-op when the program is
    /// off, the order isn't paid, it has already been credited, or the amount rounds down to zero points.
    /// </summary>
    public async Task AwardForOrderAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await platformSettings.GetAsync<PurchasePointsSettingsPayload>(
                PlatformSettingsCategoryKeys.PurchasePoints, cancellationToken);

            if (!settings.Enabled || settings.PointsPerUsd <= 0m || !IsEligible(order))
            {
                return;
            }

            var eligibleAmount = EligibleAmountUsd(order, settings.AmountBasis);
            var points = (int)Math.Min(Math.Floor(eligibleAmount * settings.PointsPerUsd), int.MaxValue);
            if (points <= 0)
            {
                return;
            }

            var key = PointsTransaction.EarnKey(order.Id);
            if (await db.PointsTransactions.AnyAsync(t => t.IdempotencyKey == key, cancellationToken))
            {
                return;
            }

            db.PointsTransactions.Add(PointsTransaction.Earn(
                order.UserId, order.Id, order.OrderNumber, points, eligibleAmount, settings.PointsPerUsd));
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Awarded {Points} purchase points to user {UserId} for order {OrderNumber}.",
                points, order.UserId, order.OrderNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A concurrent duplicate lands here via the unique key (DbUpdateException); anything else is a real fault.
            // ponytail: no retry/backfill job — an order whose award failed stays un-credited until re-completed or backfilled by hand.
            logger.LogError(ex, "Could not award purchase points for order {OrderNumber}.", order.OrderNumber);
        }
    }

    /// <summary>
    /// Takes back the points earned on an order. With no <paramref name="refundedAmountUsd"/> (cancellation or a
    /// full refund) everything still outstanding is reversed; with an amount, the share matching the refunded
    /// fraction of the order total is — cumulatively, so repeated or overlapping partial refunds never take back more
    /// than was earned. Always uses the points originally awarded, never today's rate.
    /// </summary>
    public async Task ReverseForOrderAsync(Order order, decimal? refundedAmountUsd, CancellationToken cancellationToken)
    {
        try
        {
            var earned = await db.PointsTransactions
                .Where(t => t.OrderId == order.Id && t.Type == PointsTransactionType.Earned)
                .Select(t => t.Points)
                .FirstOrDefaultAsync(cancellationToken);

            if (earned <= 0)
            {
                return;
            }

            var alreadyReversed = -await db.PointsTransactions
                .Where(t => t.OrderId == order.Id && t.Type == PointsTransactionType.Reversed)
                .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;

            var target = refundedAmountUsd is { } refunded && order.TotalAmount > 0m && refunded < order.TotalAmount
                ? (int)Math.Min(earned, Math.Round(earned * Math.Max(refunded, 0m) / order.TotalAmount, MidpointRounding.AwayFromZero))
                : earned;

            var delta = target - alreadyReversed;
            if (delta <= 0)
            {
                return;
            }

            var reason = refundedAmountUsd is { } amount && target < earned
                ? $"Order {order.OrderNumber} was partially refunded ({amount:0.00} USD)."
                : $"Order {order.OrderNumber} was cancelled or refunded.";

            db.PointsTransactions.Add(PointsTransaction.Reverse(
                order.UserId, order.Id, order.OrderNumber, delta, target, reason));
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Reversed {Points} purchase points for order {OrderNumber} (user {UserId}).",
                delta, order.OrderNumber, order.UserId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not reverse purchase points for order {OrderNumber}.", order.OrderNumber);
        }
    }

    // Paid and still live. Completed implies paid, but the payment check keeps this safe if a caller ever passes
    // an order from another path; Failed/Cancelled/Refunded/Pending never earn.
    private static bool IsEligible(Order order) =>
        order.PaymentStatus == PaymentStatus.Paid
        && order.Status is OrderStatus.Completed or OrderStatus.Processing;

    private static decimal EligibleAmountUsd(Order order, string amountBasis)
    {
        var amount = amountBasis == PurchasePointsAmountBasis.Total
            ? order.TotalAmount
            : order.Subtotal - order.DiscountAmount;

        return Math.Max(amount, 0m);
    }
}
