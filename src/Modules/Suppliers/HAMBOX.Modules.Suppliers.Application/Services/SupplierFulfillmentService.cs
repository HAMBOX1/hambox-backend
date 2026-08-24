using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Errors;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Application.Services;

/// <summary>
/// See <see cref="ISupplierFulfillmentService"/> for the contract. This implementation is the only
/// place that (a) decides when it is safe to call an <see cref="ISupplierProvider"/>, and (b) applies a
/// provider's response back onto the <see cref="SupplierFulfillment"/> state machine. Nothing here
/// branches on <c>Supplier.ProviderType</c> — the provider is always resolved generically via
/// <see cref="ISupplierProviderRegistry"/>.
/// </summary>
internal sealed class SupplierFulfillmentService(
    ISuppliersDbContext db,
    ISupplierProviderRegistry providerRegistry,
    ILogger<SupplierFulfillmentService> logger,
    ISupplierFulfillmentDeliverySink? deliverySink = null) : ISupplierFulfillmentService
{
    /// <summary>
    /// Caps how many automatic follow-up attempts a single (order, order item, supplier, mapping)
    /// scope can accumulate from repeated <see cref="SupplierFulfillmentStatus.PartialFailed"/>
    /// outcomes — without this, a permanently out-of-stock/misconfigured product would make the sweep
    /// create a new attempt every tick forever (the "avoid hot-looping" requirement).
    /// </summary>
    private const int MaxAutomaticAttemptsPerScope = 3;

    private static readonly SupplierFulfillmentStatus[] TerminalStatuses =
    [
        SupplierFulfillmentStatus.Succeeded,
        SupplierFulfillmentStatus.PartialFailed,
        SupplierFulfillmentStatus.Failed,
    ];

    public async Task<Result<SupplierFulfillmentOutcome>> RequestFulfillmentAsync(
        SupplierFulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RequestedQuantity <= 0)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.InvalidFulfillmentQuantity);
        }

        var supplier = await db.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.NotFound);
        }

        if (!supplier.IsEnabled)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.SupplierDisabled);
        }

        var mapping = await db.SupplierProductMappings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.SupplierProductMappingId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.MappingNotFound);
        }

        // IDOR guard: a mapping id supplied for one supplier must actually belong to that supplier.
        if (mapping.SupplierId != request.SupplierId)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.MappingBelongsToAnotherSupplier);
        }

        if (mapping.Status != SupplierMappingStatus.Active)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.MappingInactive);
        }

        // Idempotency seam: reuse whatever is still open for this exact scope rather than creating a
        // parallel attempt. A brand-new attempt (new HamboxReferenceId) is only ever created when no
        // open attempt exists — including the deliberate PartialFailed-follow-up case.
        var existing = await db.SupplierFulfillments
            .Where(f =>
                f.OrderId == request.OrderId &&
                f.OrderItemId == request.OrderItemId &&
                f.SupplierId == request.SupplierId &&
                f.SupplierProductMappingId == request.SupplierProductMappingId &&
                !TerminalStatuses.Contains(f.Status))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation(
                "Reusing open supplier fulfillment {FulfillmentId} (HamboxReferenceId {HamboxReferenceId}) for order item {OrderItemId} — duplicate request.",
                existing.Id, existing.HamboxReferenceId, request.OrderItemId);
            return Result.Success(ToOutcome(existing, deliveredCodes: null));
        }

        var fulfillment = SupplierFulfillment.Create(
            request.OrderId, request.OrderItemId, request.SupplierId, request.SupplierProductMappingId, request.RequestedQuantity);

        db.SupplierFulfillments.Add(fulfillment);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created supplier fulfillment {FulfillmentId} (HamboxReferenceId {HamboxReferenceId}) for order item {OrderItemId}, supplier {SupplierId}, requested quantity {Quantity}.",
            fulfillment.Id, fulfillment.HamboxReferenceId, request.OrderItemId, request.SupplierId, request.RequestedQuantity);

        return Result.Success(ToOutcome(fulfillment, deliveredCodes: null));
    }

    public async Task<Result<SupplierFulfillmentOutcome>> ProcessAsync(Guid fulfillmentId, CancellationToken cancellationToken = default)
    {
        var fulfillment = await db.SupplierFulfillments.FirstOrDefaultAsync(f => f.Id == fulfillmentId, cancellationToken);
        if (fulfillment is null)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.FulfillmentNotFound);
        }

        if (fulfillment.Status is not (SupplierFulfillmentStatus.Pending or SupplierFulfillmentStatus.Unknown))
        {
            // Not claimable right now — already in flight, already resolved, or awaiting reconciliation
            // instead. A no-op, not an error.
            return Result.Success(ToOutcome(fulfillment, deliveredCodes: null));
        }

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fulfillment.SupplierId, cancellationToken);
        if (supplier is null || !supplier.IsEnabled)
        {
            logger.LogWarning(
                "Skipping supplier fulfillment {FulfillmentId}: supplier {SupplierId} is missing or disabled.",
                fulfillment.Id, fulfillment.SupplierId);
            return Result.Failure<SupplierFulfillmentOutcome>(supplier is null ? SupplierErrors.NotFound : SupplierErrors.SupplierDisabled);
        }

        var mapping = await db.SupplierProductMappings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == fulfillment.SupplierProductMappingId, cancellationToken);
        if (mapping is null || mapping.Status != SupplierMappingStatus.Active)
        {
            logger.LogWarning(
                "Skipping supplier fulfillment {FulfillmentId}: mapping {MappingId} is missing or inactive.",
                fulfillment.Id, fulfillment.SupplierProductMappingId);
            return Result.Failure<SupplierFulfillmentOutcome>(mapping is null ? SupplierErrors.MappingNotFound : SupplierErrors.MappingInactive);
        }

        var providerResolution = providerRegistry.Resolve(supplier.ProviderType);
        if (providerResolution.IsFailure)
        {
            logger.LogWarning(
                "Skipping supplier fulfillment {FulfillmentId}: no provider registered for type '{ProviderType}'.",
                fulfillment.Id, supplier.ProviderType);
            return Result.Failure<SupplierFulfillmentOutcome>(providerResolution.Error);
        }

        var provider = providerResolution.Value;

        // --- The claim: this write — and specifically its optimistic-concurrency check — is the entire
        // double-purchase guard. Two workers can both reach this point for the same row; only the first
        // SaveChangesAsync to commit wins. ---
        fulfillment.Claim();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation(
                "Lost the claim race for supplier fulfillment {FulfillmentId} — another worker owns this attempt.",
                fulfillment.Id);
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.ConcurrentClaimLost);
        }

        // --- Only now that the claim has committed is it safe to call the external provider. No SQL
        // transaction is open across this call — the claim above and the result-save below are each
        // their own SaveChangesAsync. ---
        var context = BuildContext(supplier);
        var purchaseRequest = new SupplierPurchaseRequest(
            mapping.ExternalProductId,
            fulfillment.RequestedQuantity,
            ProviderReservationId: null,
            ReferenceId: fulfillment.HamboxReferenceId.ToString(),
            UnitFaceValue: mapping.BuyingPrice,
            Currency: mapping.Currency);

        SupplierPurchaseResult result;
        try
        {
            result = await provider.PurchaseAsync(purchaseRequest, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Ambiguous: we do not know whether the provider received/accepted the request. Never
            // assume failure, never retry blindly — only reconciliation via GetOrderStatusAsync may
            // resolve this from here.
            logger.LogWarning(ex,
                "Supplier purchase call was ambiguous for fulfillment {FulfillmentId} (HamboxReferenceId {HamboxReferenceId}, supplier {SupplierId}) — marking Unknown for reconciliation.",
                fulfillment.Id, fulfillment.HamboxReferenceId, fulfillment.SupplierId);
            fulfillment.MarkUnknown();
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(ToOutcome(fulfillment, deliveredCodes: null));
        }

        ApplyPurchaseResult(fulfillment, result);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyDeliverySinkAsync(fulfillment, result.DeliveredCodes, cancellationToken);

        return Result.Success(ToOutcome(fulfillment, result.DeliveredCodes));
    }

    public async Task<Result<SupplierFulfillmentOutcome>> ReconcileAsync(Guid fulfillmentId, CancellationToken cancellationToken = default)
    {
        var fulfillment = await db.SupplierFulfillments.FirstOrDefaultAsync(f => f.Id == fulfillmentId, cancellationToken);
        if (fulfillment is null)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.FulfillmentNotFound);
        }

        // Submitting is included here deliberately: a worker that claimed but crashed before submitting
        // (or before persisting the submission outcome) leaves the row stuck in Submitting forever
        // unless reconciliation can resolve it — which it can, because the provider is queried by the
        // stable HamboxReferenceId, not by a provider order id that may never have been recorded.
        if (fulfillment.Status is not (SupplierFulfillmentStatus.Submitting or SupplierFulfillmentStatus.Submitted or SupplierFulfillmentStatus.Unknown))
        {
            return Result.Success(ToOutcome(fulfillment, deliveredCodes: null));
        }

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fulfillment.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(SupplierErrors.NotFound);
        }

        var providerResolution = providerRegistry.Resolve(supplier.ProviderType);
        if (providerResolution.IsFailure)
        {
            return Result.Failure<SupplierFulfillmentOutcome>(providerResolution.Error);
        }

        var context = BuildContext(supplier);
        var query = new SupplierOrderStatusQuery(fulfillment.HamboxReferenceId, fulfillment.ProviderOrderId);

        SupplierOrderStatusResult result;
        try
        {
            result = await providerResolution.Value.GetOrderStatusAsync(query, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Reconciliation query failed for supplier fulfillment {FulfillmentId} (HamboxReferenceId {HamboxReferenceId}) — will retry later.",
                fulfillment.Id, fulfillment.HamboxReferenceId);
            fulfillment.RecordReconciliationAttempt();
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(ToOutcome(fulfillment, deliveredCodes: null));
        }

        ApplyStatusResult(fulfillment, result);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyDeliverySinkAsync(fulfillment, result.DeliveredCodes, cancellationToken);

        return Result.Success(ToOutcome(fulfillment, result.DeliveredCodes));
    }

    /// <summary>
    /// The one place delivered codes are handed off — called after the terminal status has already
    /// been committed, from both the synchronous purchase path and the background sweep's
    /// reconciliation path, so a consumer (Commerce) receives codes exactly once regardless of which
    /// path resolved them. Fires whenever the fulfillment reaches ANY terminal state
    /// (Succeeded/PartialFailed/Failed) — not only when codes are present — so a consumer can react to
    /// a definite zero-delivery outcome too (e.g. Commerce's "supplier first" fallback-to-manual, which
    /// must only ever trigger from a definite terminal state, never from Unknown/Submitting/Submitted).
    /// <paramref name="deliveredCodes"/> is passed through as-is (possibly null or empty) — the sink
    /// implementation already treats an empty collection as a safe no-op for the code-attachment side
    /// of its own work. If no sink is registered, or the fulfillment is not yet terminal, this is a
    /// no-op. A sink failure is logged but never rethrown — the purchase itself genuinely resolved and
    /// reverting SupplierFulfillment's status would misrepresent what happened with the supplier and
    /// risk a duplicate purchase on retry; this is a documented, accepted limitation of not having a
    /// single distributed transaction across the two databases — see docs/integrations/suppliers/README.md.
    /// </summary>
    private async Task NotifyDeliverySinkAsync(SupplierFulfillment fulfillment, IReadOnlyCollection<string>? deliveredCodes, CancellationToken cancellationToken)
    {
        if (deliverySink is null || !fulfillment.IsTerminal)
        {
            return;
        }

        try
        {
            // Computed here (not left to the sink to ask for) specifically to avoid the sink needing a
            // dependency on ISupplierFulfillmentService — this class is the one production
            // implementation of that interface and also depends on whichever sink is registered, so a
            // sink resolving ISupplierFulfillmentService itself would be a circular DI dependency.
            var isScopeExhausted = await IsScopeExhaustedAsync(
                fulfillment.OrderId, fulfillment.OrderItemId, fulfillment.SupplierId, fulfillment.SupplierProductMappingId, cancellationToken);

            await deliverySink.OnDeliveredAsync(
                fulfillment.OrderId, fulfillment.OrderItemId, fulfillment.SupplierId, fulfillment.SupplierProductMappingId,
                isScopeExhausted, deliveredCodes ?? [], cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Supplier fulfillment {FulfillmentId} (order {OrderId}, item {OrderItemId}) succeeded with the provider but delivering the codes to the order failed — manual intervention required.",
                fulfillment.Id, fulfillment.OrderId, fulfillment.OrderItemId);
        }
    }

    public async Task<SupplierFulfillmentSweepResult> ProcessDueFulfillmentsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(batchSize, 1, 200);
        int examined = 0, submitted = 0, reconciled = 0, followUps = 0, lostRace = 0, errors = 0;

        var pendingIds = await db.SupplierFulfillments.AsNoTracking()
            .Where(f => f.Status == SupplierFulfillmentStatus.Pending)
            .OrderBy(f => f.CreatedOnUtc)
            .Take(size)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
        {
            examined++;
            try
            {
                var result = await ProcessAsync(id, cancellationToken);
                if (result.IsSuccess)
                {
                    submitted++;
                }
                else if (result.Error == SupplierErrors.ConcurrentClaimLost)
                {
                    lostRace++;
                }
                else
                {
                    errors++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(ex, "Unhandled error processing supplier fulfillment {FulfillmentId} during sweep.", id);
            }
        }

        var needsReconciliationIds = await db.SupplierFulfillments.AsNoTracking()
            .Where(f => f.Status == SupplierFulfillmentStatus.Submitting
                     || f.Status == SupplierFulfillmentStatus.Submitted
                     || f.Status == SupplierFulfillmentStatus.Unknown)
            .OrderBy(f => f.LastReconciledOnUtc ?? f.CreatedOnUtc)
            .Take(size)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in needsReconciliationIds)
        {
            examined++;
            try
            {
                var result = await ReconcileAsync(id, cancellationToken);
                if (result.IsSuccess)
                {
                    reconciled++;
                }
                else
                {
                    errors++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(ex, "Unhandled error reconciling supplier fulfillment {FulfillmentId} during sweep.", id);
            }
        }

        var partialFailed = await db.SupplierFulfillments.AsNoTracking()
            .Where(f => f.Status == SupplierFulfillmentStatus.PartialFailed)
            .OrderBy(f => f.CompletedOnUtc)
            .Take(size)
            .ToListAsync(cancellationToken);

        foreach (var attempt in partialFailed)
        {
            if (attempt.RemainingQuantity <= 0)
            {
                continue;
            }

            examined++;
            try
            {
                if (await TryCreateFollowUpAsync(attempt, cancellationToken))
                {
                    followUps++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(ex, "Unhandled error creating a follow-up for supplier fulfillment {FulfillmentId} during sweep.", attempt.Id);
            }
        }

        return new SupplierFulfillmentSweepResult(examined, submitted, reconciled, followUps, lostRace, errors);
    }

    public async Task<bool> IsScopeExhaustedAsync(
        Guid orderId, Guid orderItemId, Guid supplierId, Guid supplierProductMappingId, CancellationToken cancellationToken = default)
    {
        var attempts = await db.SupplierFulfillments.AsNoTracking()
            .Where(f => f.OrderId == orderId && f.OrderItemId == orderItemId
                && f.SupplierId == supplierId && f.SupplierProductMappingId == supplierProductMappingId)
            .Select(f => new { f.Status, f.CreatedOnUtc })
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0 || attempts.Any(f => !TerminalStatuses.Contains(f.Status)))
        {
            // Nothing attempted yet, or something is still in flight/ambiguous — never exhausted while
            // an attempt could still resolve or be automatically retried.
            return false;
        }

        var latestStatus = attempts.OrderByDescending(f => f.CreatedOnUtc).First().Status;

        // A pure Failed attempt (zero delivered) never gets an automatic follow-up — see
        // ProcessDueFulfillmentsAsync's PartialFailed-only follow-up loop — so it's exhausted the
        // instant it becomes terminal. PartialFailed/Succeeded are only exhausted once the same
        // attempt-count cap TryCreateFollowUpAsync enforces has been reached.
        return latestStatus == SupplierFulfillmentStatus.Failed || attempts.Count >= MaxAutomaticAttemptsPerScope;
    }

    private async Task<bool> TryCreateFollowUpAsync(SupplierFulfillment attempt, CancellationToken cancellationToken)
    {
        var scopeAttemptCount = await db.SupplierFulfillments.AsNoTracking().CountAsync(
            f => f.OrderId == attempt.OrderId
              && f.OrderItemId == attempt.OrderItemId
              && f.SupplierId == attempt.SupplierId
              && f.SupplierProductMappingId == attempt.SupplierProductMappingId,
            cancellationToken);

        if (scopeAttemptCount >= MaxAutomaticAttemptsPerScope)
        {
            logger.LogWarning(
                "Not auto-creating a follow-up for supplier fulfillment {FulfillmentId}: {AttemptCount} attempts already exist for order item {OrderItemId} — needs manual attention.",
                attempt.Id, scopeAttemptCount, attempt.OrderItemId);
            return false;
        }

        var request = new SupplierFulfillmentRequest(
            attempt.OrderId, attempt.OrderItemId, attempt.SupplierId, attempt.SupplierProductMappingId, attempt.RemainingQuantity);

        var result = await RequestFulfillmentAsync(request, cancellationToken);
        return result.IsSuccess;
    }

    private static void ApplyPurchaseResult(SupplierFulfillment fulfillment, SupplierPurchaseResult result)
    {
        if (!result.IsSuccess)
        {
            fulfillment.MarkFailed(
                result.FailureCategory ?? SupplierFulfillmentFailureCategory.UnknownProviderState,
                result.Message,
                result.ProviderOrderId);
            return;
        }

        if (result.DeliveredCodes is null)
        {
            // Accepted, outcome not yet known — the documented async-provider shape (e.g. Bamboo's
            // Place Order). Needs a later reconciliation call to resolve.
            if (string.IsNullOrWhiteSpace(result.ProviderOrderId))
            {
                // Malformed: success with nothing to track it by afterward. Cannot trust it.
                fulfillment.MarkUnknown();
                return;
            }

            fulfillment.MarkSubmitted(result.ProviderOrderId);
            return;
        }

        ApplyDeliveredOutcome(fulfillment, result.DeliveredCodes.Count, result.ProviderOrderId);
    }

    private static void ApplyStatusResult(SupplierFulfillment fulfillment, SupplierOrderStatusResult result)
    {
        switch (result.Status)
        {
            case SupplierProviderOrderStatus.Succeeded:
                if (result.DeliveredCodes is not null && result.DeliveredCodes.Count == fulfillment.RequestedQuantity)
                {
                    fulfillment.MarkSucceeded(result.DeliveredCodes.Count, result.ProviderOrderId);
                }
                else
                {
                    fulfillment.MarkFailed(
                        SupplierFulfillmentFailureCategory.UnknownProviderState,
                        "Provider reported Succeeded but the delivered item count did not match the requested quantity.",
                        result.ProviderOrderId);
                }
                break;

            case SupplierProviderOrderStatus.PartialFailed:
                if (result.DeliveredCodes is not null && result.DeliveredCodes.Count > 0 && result.DeliveredCodes.Count < fulfillment.RequestedQuantity)
                {
                    fulfillment.MarkPartialFailed(result.DeliveredCodes.Count, result.ProviderOrderId);
                }
                else
                {
                    fulfillment.MarkFailed(
                        SupplierFulfillmentFailureCategory.UnknownProviderState,
                        "Provider reported PartialFailed with an inconsistent delivered item count.",
                        result.ProviderOrderId);
                }
                break;

            case SupplierProviderOrderStatus.Failed:
                fulfillment.MarkFailed(
                    result.FailureCategory ?? SupplierFulfillmentFailureCategory.UnknownProviderState,
                    result.Message,
                    result.ProviderOrderId);
                break;

            case SupplierProviderOrderStatus.Pending:
            case SupplierProviderOrderStatus.Processing:
            case SupplierProviderOrderStatus.Unknown:
            default:
                // Still unresolved. A Submitting row reaching here (crash recovery) has no domain
                // transition for "confirmed still ambiguous" other than Unknown, so promote it
                // explicitly; Submitted/Unknown rows just record the attempt and stay put.
                if (fulfillment.Status == SupplierFulfillmentStatus.Submitting)
                {
                    fulfillment.MarkUnknown();
                }

                fulfillment.RecordReconciliationAttempt();
                break;
        }
    }

    private static void ApplyDeliveredOutcome(SupplierFulfillment fulfillment, int deliveredCount, string? providerOrderId)
    {
        if (deliveredCount == fulfillment.RequestedQuantity)
        {
            fulfillment.MarkSucceeded(deliveredCount, providerOrderId);
        }
        else if (deliveredCount > 0 && deliveredCount < fulfillment.RequestedQuantity)
        {
            fulfillment.MarkPartialFailed(deliveredCount, providerOrderId);
        }
        else
        {
            // 0, or more than requested — never trust either; fail closed rather than guess.
            fulfillment.MarkFailed(
                SupplierFulfillmentFailureCategory.UnknownProviderState,
                $"Provider reported {deliveredCount} delivered item(s) against a requested quantity of {fulfillment.RequestedQuantity}.",
                providerOrderId);
        }
    }

    private static SupplierProviderContext BuildContext(Supplier supplier) => new(
        supplier.Id,
        supplier.Code,
        supplier.BaseUrl,
        new SupplierProviderCredentials(supplier.ApiKey, supplier.ApiSecret, supplier.Username, supplier.Password, supplier.BearerToken, supplier.OAuthSettingsJson),
        supplier.SettingsJson);

    private static SupplierFulfillmentOutcome ToOutcome(SupplierFulfillment fulfillment, IReadOnlyCollection<string>? deliveredCodes) => new(
        fulfillment.Id,
        fulfillment.HamboxReferenceId,
        fulfillment.Status,
        fulfillment.RequestedQuantity,
        fulfillment.DeliveredQuantity,
        fulfillment.RemainingQuantity,
        deliveredCodes,
        fulfillment.FailureCategory);
}
