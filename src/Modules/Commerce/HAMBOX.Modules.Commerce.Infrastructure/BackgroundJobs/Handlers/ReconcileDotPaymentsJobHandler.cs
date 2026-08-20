using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Safety net for DOT payment attempts whose browser callback and provider webhook both never
/// arrived (network blip, ad-blocker, DOT-side hiccup) — re-verifies every still-Pending attempt
/// with DOT directly, then gives up on whatever is left Pending past its reservation window,
/// marking it <see cref="PaymentAttemptStatus.Expired"/> so it stops showing as "processing" to the
/// customer. Runs every 5 minutes (see <c>OperationalJobWorker.RegisterBuiltInRecurringJobs</c>).
/// </summary>
internal sealed class ReconcileDotPaymentsJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext commerceDbContext,
    DotPaymentVerificationService verificationService,
    ILogger<ReconcileDotPaymentsJobHandler> logger)
    : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ReconcileDotPayments;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var pendingIds = await commerceDbContext.PaymentAttempts
            .Where(p => p.Provider == "Dot" && p.Status == PaymentAttemptStatus.Pending)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
        {
            var result = await verificationService.VerifyAndFinalizeAsync(id, cancellationToken);
            if (result.Outcome != DotVerificationOutcome.StillPending)
            {
                logger.LogInformation("DOT reconciliation resolved payment attempt {PaymentAttemptId} as {Outcome}.", id, result.Outcome);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var stillStale = await commerceDbContext.PaymentAttempts
            .Where(p => p.Provider == "Dot" && p.Status == PaymentAttemptStatus.Pending && p.ExpiresOnUtc < now)
            .ToListAsync(cancellationToken);

        foreach (var attempt in stillStale)
        {
            attempt.MarkExpired();
        }

        if (stillStale.Count > 0)
        {
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("DOT reconciliation expired {Count} unresolved payment attempt(s).", stillStale.Count);
        }
    }
}
