using HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Dot;

public sealed class DotPaymentVerificationServiceTests
{
    private static async Task<(DotTestHarness Harness, Guid PaymentAttemptId, Guid OrderId)> InitiateAsync(
        int stock = 5, decimal price = 10m)
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock, price);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return (harness, result.Value.PaymentAttemptId, result.Value.OrderId);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_SuccessfulTransaction_CapturesPaymentAndDeliversCode()
    {
        var (harness, attemptId, orderId) = await InitiateAsync(stock: 5, price: 10m);
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Succeeded, result.Outcome);

        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal("Dot", order.PaymentProvider);

        // Pre-existing OrderFulfillmentService behavior (see OrderFulfillmentServiceTests): its
        // "is the order now fully delivered" check queries OrderLicenseKeys before this call's own
        // SaveChangesAsync, so a single-call full delivery is left Processing, not Completed, here
        // — the enqueued RetryOrderFulfillment job (asserted below) picks it up and completes it.
        // Not this task's bug to fix; the payment-capture/delivery split below is what matters.
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Contains(OperationalJobTypes.RetryOrderFulfillment, harness.JobQueue.EnqueuedJobTypes);

        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
        Assert.Single(harness.Communication.SentRequests);
        Assert.Equal("OrderConfirmation", harness.Communication.SentRequests[0].TemplateKey);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(10m, attempt.VerifiedAmount);
    }

    [Theory]
    [InlineData(1001, "Invalid PIN entered by user")]
    [InlineData(1002, "OTP session timed out")]
    [InlineData(1004, "Insufficient balance")]
    [InlineData(1011, "Invalid MSISDN")]
    [InlineData(1015, "transaction not found")]
    public async Task VerifyAndFinalizeAsync_UnsuccessfulTransaction_MarksOrderFailedAndDeliversNothing(
        int resultCode, string resultDesc)
    {
        var (harness, attemptId, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new(resultCode, resultDesc, null, null, null);

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Failed, result.Outcome);

        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(PaymentStatus.Failed, order.PaymentStatus);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
        Assert.Empty(harness.Communication.SentRequests);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_AmountMismatch_NeverCompletesOrder()
    {
        var (harness, attemptId, orderId) = await InitiateAsync(price: 10m);
        // DOT itself reports "successful" but for a different amount than HAMBOX expected —
        // must never be trusted into completing the order.
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 999m, "USD");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Failed, result.Outcome);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.NotEqual(OrderStatus.Completed, order.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_CurrencyMismatch_NeverCompletesOrder()
    {
        var (harness, attemptId, orderId) = await InitiateAsync(price: 10m);
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "PKR");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Failed, result.Outcome);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.NotEqual(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_CalledTwiceAfterSuccess_IsIdempotentAndDoesNotDoubleFulfillOrNotify()
    {
        var (harness, attemptId, orderId) = await InitiateAsync(stock: 5, price: 10m);
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        var first = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);
        var second = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Succeeded, first.Outcome);
        Assert.Equal(DotVerificationOutcome.Succeeded, second.Outcome);

        // Only the first call actually talked to DOT; the second replayed the cached terminal state.
        Assert.Single(harness.Gateway.StatusCheckCalls);
        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
        Assert.Single(harness.Communication.SentRequests);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_NotificationReplayAfterFailure_IsIdempotent()
    {
        var (harness, attemptId, _) = await InitiateAsync();
        harness.Gateway.StatusResult = new(1001, "Invalid PIN entered by user", null, null, null);

        var first = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);
        var second = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Failed, first.Outcome);
        Assert.Equal(DotVerificationOutcome.Failed, second.Outcome);
        Assert.Single(harness.Gateway.StatusCheckCalls);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_AlreadyBeingVerifiedConcurrently_ShortCircuitsWithoutCallingProviderAgain()
    {
        var (harness, attemptId, _) = await InitiateAsync();

        // Simulate another caller (webhook) having already won the guarded claim.
        var claimant = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        claimant.BeginVerification();
        await harness.CommerceDb.SaveChangesAsync(CancellationToken.None);

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.StillPending, result.Outcome);
        Assert.Empty(harness.Gateway.StatusCheckCalls);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_ProviderTimeout_ReleasesClaimAndLeavesOrderPending()
    {
        var (harness, attemptId, orderId) = await InitiateAsync();
        harness.Gateway.FailStatusCheck = true;

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.StillPending, result.Outcome);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_UnknownPaymentAttempt_ReturnsNotFound()
    {
        var harness = DotTestHarness.Create();

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_NeverAffectsAnUnrelatedOrder()
    {
        var (harnessA, attemptIdA, orderIdA) = await InitiateAsync(price: 10m);

        // A second, wholly unrelated Dot order/attempt in the same store (constructed directly —
        // the point of this test is isolation during finalize, not another full initiate flow).
        var orderB = Order.Create(
            "user-2", $"ORD-{Guid.NewGuid():N}", "buyer2@example.com", "US", "dot",
            25m, 0m, 0m, 25m, [(Guid.NewGuid(), "Other Product", 1, 25m, (Guid?)null, (string?)null)]);
        var attemptB = PaymentAttempt.CreatePendingDot(
            orderB.Id, "partner-txid-B", "21", "1", 25m, "USD", DateTimeOffset.UtcNow.AddMinutes(30), null);
        harnessA.CommerceDb.Orders.Add(orderB);
        harnessA.CommerceDb.PaymentAttempts.Add(attemptB);
        await harnessA.CommerceDb.SaveChangesAsync(CancellationToken.None);

        harnessA.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");
        var result = await harnessA.VerificationService.VerifyAndFinalizeAsync(attemptIdA, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Succeeded, result.Outcome);
        var orderAAfter = await harnessA.CommerceDb.Orders.FirstAsync(o => o.Id == orderIdA);
        var orderBAfter = await harnessA.CommerceDb.Orders.FirstAsync(o => o.Id == orderB.Id);
        Assert.Equal(PaymentStatus.Paid, orderAAfter.PaymentStatus);
        Assert.Equal(OrderStatus.Pending, orderBAfter.Status);
        Assert.Equal(PaymentStatus.Pending, orderBAfter.PaymentStatus);

        var attemptBAfter = await harnessA.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptB.Id);
        Assert.Equal(PaymentAttemptStatus.Pending, attemptBAfter.Status);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_StockGoneWhenFulfilling_StillCapturesPaymentAndEnqueuesRetry()
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 1, price: 10m);
        await harness.SeedCartAsync(product, variant, quantity: 1);

        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash"), CancellationToken.None);
        Assert.True(initiation.IsSuccess);

        // Stock disappears between initiation and confirmation (e.g. another channel sold it) —
        // OrderFulfillmentService.FulfillMissingAsync throws InvalidOperationException in this
        // case. The captured payment must survive that; delivery is left for the retry job.
        harness.InventoryEngine.AvailableStockByVariant[variant.Id] = 0;
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(
            initiation.Value.PaymentAttemptId, CancellationToken.None);

        Assert.Equal(DotVerificationOutcome.Succeeded, result.Outcome);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == initiation.Value.OrderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.NotEqual(OrderStatus.Completed, order.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
        Assert.Contains(OperationalJobTypes.RetryOrderFulfillment, harness.JobQueue.EnqueuedJobTypes);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == initiation.Value.PaymentAttemptId);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
    }
}
