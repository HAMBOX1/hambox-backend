using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Points;

/// <param name="Available">Spendable now (settled credits minus settled and reserved debits).</param>
/// <param name="Pending">Credited but not yet spendable (awaiting hold/review).</param>
/// <param name="Reserved">Held by redemptions in flight.</param>
/// <param name="LifetimeEarned">Everything ever credited (excluding cancelled rows).</param>
public sealed record StarsBalance(int Available, int Pending, int Reserved, int LifetimeEarned);

/// <summary>
/// The one place that reads and writes the Stars ledger. Time-driven transitions (a hold passing, a credit
/// expiring) are applied lazily whenever a user's balance is read or touched, so they need no scheduler and can't be
/// missed. ponytail: lazy per-user maintenance; add a sweep job if "expired" reports must be exact for idle users.
/// </summary>
public sealed class StarsLedgerService(ICommerceDbContext db)
{
    public async Task<StarsBalance> GetBalanceAsync(string userId, CancellationToken cancellationToken)
    {
        await MaintainAsync(userId, cancellationToken);

        var rows = db.PointsTransactions.AsNoTracking().Where(t => t.UserId == userId);
        var available = await rows
            .Where(t => t.Status == StarsStatus.Available || t.Status == StarsStatus.Reserved)
            .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;
        var pending = await rows.Where(t => t.Status == StarsStatus.Pending).SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;
        var reserved = -await rows.Where(t => t.Status == StarsStatus.Reserved).SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;
        var lifetime = await rows
            .Where(t => t.Points > 0 && t.Status != StarsStatus.Cancelled
                && (t.Type == PointsTransactionType.Earned || t.Type == PointsTransactionType.ReferralReward || t.Type == PointsTransactionType.Bonus))
            .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;

        return new StarsBalance(available, pending, reserved, lifetime);
    }

    /// <summary>
    /// Idempotent credit from a non-order source (referral, bonus, redemption refund). Returns false when the same
    /// effect was already recorded. Honors the configured hold/expiry through the explicit arguments.
    /// </summary>
    public async Task<bool> CreditAsync(
        string userId, PointsTransactionType type, int points, string description, string idempotencyKey,
        StarsStatus status = StarsStatus.Available, DateTimeOffset? availableOnUtc = null, DateTimeOffset? expiresOnUtc = null,
        string? reference = null, CancellationToken cancellationToken = default)
    {
        if (points <= 0 || await db.PointsTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return false;
        }

        db.PointsTransactions.Add(PointsTransaction.Credit(
            userId, type, points, description, idempotencyKey, status, availableOnUtc, expiresOnUtc, reference: reference));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Idempotent debit that may take the balance negative (a rightful clawback of already-spent Stars).</summary>
    public async Task<bool> DebitAsync(
        string userId, PointsTransactionType type, int points, string description, string idempotencyKey,
        string? reference = null, CancellationToken cancellationToken = default)
    {
        if (points <= 0 || await db.PointsTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return false;
        }

        db.PointsTransactions.Add(PointsTransaction.Debit(userId, type, points, description, idempotencyKey, reference: reference));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Holds <paramref name="stars"/> for an order (a Reserved debit): they stop being spendable immediately but are not
    /// final until the order completes. False when the customer can't cover it. Idempotent per order.
    /// </summary>
    public async Task<bool> ReserveForOrderAsync(
        string userId, int stars, Guid orderId, string orderNumber, string description, CancellationToken cancellationToken)
    {
        var key = RedeemKey(orderId);
        if (await db.PointsTransactions.AnyAsync(t => t.IdempotencyKey == key, cancellationToken))
        {
            return true;
        }

        if ((await GetBalanceAsync(userId, cancellationToken)).Available < stars)
        {
            return false;
        }

        db.PointsTransactions.Add(PointsTransaction.Debit(
            userId, PointsTransactionType.Redeemed, stars, description, key, StarsStatus.Reserved, orderId, orderNumber));
        return true;
    }

    /// <summary>The order was delivered: a Reserved redemption becomes final.</summary>
    public async Task SettleForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var row = await db.PointsTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == RedeemKey(orderId), cancellationToken);
        if (row is not null && row.Settle())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The order was cancelled/refunded: a still-Reserved redemption is voided, a settled one is paid back as a
    /// RedemptionRefund credit. Idempotent.
    /// </summary>
    public async Task ReleaseForOrderAsync(Guid orderId, string userId, CancellationToken cancellationToken)
    {
        var row = await db.PointsTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == RedeemKey(orderId), cancellationToken);
        if (row is null)
        {
            return;
        }

        if (row.Status == StarsStatus.Reserved)
        {
            row.Cancel();
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (row.Status == StarsStatus.Available)
        {
            await CreditAsync(
                userId, PointsTransactionType.RedemptionRefund, -row.Points, "Stars returned for a refunded redemption.",
                $"redeem-refund:{orderId:N}", cancellationToken: cancellationToken);
        }
    }

    public static string RedeemKey(Guid orderId) => $"redeem:{orderId:N}";

    /// <summary>Manual signed correction. A negative adjustment can never take the spendable balance below zero.</summary>
    public async Task<bool> AdjustAsync(
        string userId, int signedPoints, string reason, string actorUserId, CancellationToken cancellationToken)
    {
        if (signedPoints < 0 && (await GetBalanceAsync(userId, cancellationToken)).Available + signedPoints < 0)
        {
            return false;
        }

        db.PointsTransactions.Add(PointsTransaction.Adjust(userId, signedPoints, reason, actorUserId));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task MaintainAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        var due = await db.PointsTransactions
            .Where(t => t.UserId == userId && t.Status == StarsStatus.Pending && t.AvailableOnUtc != null && t.AvailableOnUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (var row in due)
        {
            changed |= row.Release();
        }

        var lapsed = await db.PointsTransactions
            .Where(t => t.UserId == userId && t.Status == StarsStatus.Available && t.Points > 0
                && t.ExpiresOnUtc != null && t.ExpiresOnUtc <= now)
            .ToListAsync(cancellationToken);
        if (lapsed.Count > 0)
        {
            var spendable = await db.PointsTransactions
                .Where(t => t.UserId == userId && (t.Status == StarsStatus.Available || t.Status == StarsStatus.Reserved))
                .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;

            foreach (var credit in lapsed.OrderBy(c => c.ExpiresOnUtc))
            {
                var key = $"expire:{credit.Id:N}";
                if (await db.PointsTransactions.AnyAsync(t => t.IdempotencyKey == key, cancellationToken))
                {
                    continue;
                }

                // Only what is still unspent can lapse (spent Stars are consumed from the balance first).
                var amount = Math.Min(credit.Points, spendable);
                if (amount <= 0)
                {
                    continue;
                }

                db.PointsTransactions.Add(PointsTransaction.Debit(
                    userId, PointsTransactionType.Expired, amount, "Stars expired.", key, reference: credit.Id.ToString()));
                spendable -= amount;
                changed = true;
            }
        }

        // A reservation whose order failed or was cancelled before it ever completed is released here, so no gateway
        // failure path has to remember to do it.
        var reserved = await db.PointsTransactions
            .Where(t => t.UserId == userId && t.Status == StarsStatus.Reserved && t.OrderId != null)
            .ToListAsync(cancellationToken);
        if (reserved.Count > 0)
        {
            var orderIds = reserved.Select(r => r.OrderId!.Value).ToList();
            var dead = (await db.Orders.AsNoTracking()
                    .Where(o => orderIds.Contains(o.Id) && (o.Status == OrderStatus.Failed || o.Status == OrderStatus.Cancelled))
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
            foreach (var row in reserved.Where(r => dead.Contains(r.OrderId!.Value)))
            {
                changed |= row.Cancel();
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
