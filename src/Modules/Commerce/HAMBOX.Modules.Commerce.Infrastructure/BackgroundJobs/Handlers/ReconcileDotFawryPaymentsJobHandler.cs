using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Safety net for DOT Fawry payment attempts whose notification webhook never arrived (network
/// blip, DOT-side hiccup, or the customer simply never went to pay at Fawry) — re-verifies every
/// still-Pending attempt with DOT directly, then gives up on whatever is left Pending past its
/// reservation window, marking it <see cref="PaymentAttemptStatus.Expired"/> so it stops showing as
/// "awaiting payment" to the customer. Runs every 5 minutes (see
/// <c>OperationalJobWorker.RegisterBuiltInRecurringJobs</c>). A sibling of
/// <c>ReconcileDotPaymentsJobHandler</c> for a different DOT product — kept separate so the existing
/// carrier-billing integration is never touched by changes here.
/// </summary>
internal sealed class ReconcileDotFawryPaymentsJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext commerceDbContext,
    DotFawryPaymentVerificationService verificationService,
    ILogger<ReconcileDotFawryPaymentsJobHandler> logger)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ReconcileDotFawryPayments;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var pendingIds = await commerceDbContext.PaymentAttempts
            .Where(p => p.Provider == "DotFawry" && p.Status == PaymentAttemptStatus.Pending)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
        {
            var result = await verificationService.VerifyAndFinalizeAsync(id, cancellationToken);
            if (result.Outcome != DotFawryVerificationOutcome.StillPending)
            {
                logger.LogInformation("DOT Fawry reconciliation resolved payment attempt {PaymentAttemptId} as {Outcome}.", id, result.Outcome);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var stillStale = await commerceDbContext.PaymentAttempts
            .Where(p => p.Provider == "DotFawry" && p.Status == PaymentAttemptStatus.Pending && p.ExpiresOnUtc < now)
            .ToListAsync(cancellationToken);

        foreach (var attempt in stillStale)
        {
            attempt.MarkExpired();
        }

        if (stillStale.Count > 0)
        {
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("DOT Fawry reconciliation expired {Count} unresolved payment attempt(s).", stillStale.Count);
        }
    }
}
