using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Safety net for Cryptomus payment attempts whose webhook was rejected, lost or never delivered (a paid
/// invoice would otherwise be finalised only if the customer's browser kept polling the result page):
/// re-verifies every still-Pending attempt directly with Cryptomus through the same idempotent, claim-guarded
/// verification service the webhook and the status poll use. Runs every 5 minutes (see
/// <c>OperationalJobWorker.RegisterBuiltInRecurringJobs</c>).
/// <para>
/// Unlike the DOT sweeps this never gives up on an attempt at the local payment window: a crypto customer can
/// legitimately pay on-chain after it, and expiring the attempt locally would turn that paid invoice into a
/// "window expired" failure. The provider's own terminal statuses (expired / cancelled / failed) are what resolve
/// an attempt; only an attempt still unresolved a full day after its window is expired here.
/// </para>
/// </summary>
internal sealed class ReconcileCryptomusPaymentsJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext commerceDbContext,
    CryptomusPaymentVerificationService verificationService,
    ILogger<ReconcileCryptomusPaymentsJobHandler> logger,
    OrderStockHoldService? stockHold = null)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    private static readonly TimeSpan GiveUpAfterWindow = TimeSpan.FromHours(24);

    public override string JobType => OperationalJobTypes.ReconcileCryptomusPayments;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var pendingIds = await commerceDbContext.PaymentAttempts
            .Where(p => p.Provider == "Cryptomus" && p.Status == PaymentAttemptStatus.Pending)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
        {
            try
            {
                var result = await verificationService.VerifyAndFinalizeAsync(id, cancellationToken);
                if (result.Outcome != CryptomusVerificationOutcome.StillPending)
                {
                    logger.LogInformation(
                        "Cryptomus reconciliation resolved payment attempt {PaymentAttemptId} as {Outcome}.", id, result.Outcome);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One attempt's provider hiccup must not stop the sweep for the rest.
                logger.LogWarning(ex, "Cryptomus reconciliation could not verify payment attempt {PaymentAttemptId}.", id);
            }
        }

        var cutoff = DateTimeOffset.UtcNow - GiveUpAfterWindow;
        var abandoned = await commerceDbContext.PaymentAttempts
            .Where(p => p.Provider == "Cryptomus" && p.Status == PaymentAttemptStatus.Pending && p.ExpiresOnUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var attempt in abandoned)
        {
            attempt.MarkExpired();
            if (stockHold is not null)
            {
                await stockHold.ReleaseAsync(attempt.OrderId, "Payment window expired", cancellationToken);
            }
        }

        if (abandoned.Count > 0)
        {
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Cryptomus reconciliation expired {Count} abandoned payment attempt(s).", abandoned.Count);
        }
    }
}
