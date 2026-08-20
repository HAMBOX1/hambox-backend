using HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.UnitTests.Commerce.DotFawry.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.DotFawry;

public sealed class DotFawryPaymentVerificationServiceTests
{
    private static async Task<(DotFawryTestHarness Harness, Guid PaymentAttemptId, Guid OrderId)> InitiateAsync(
        int stock = 5, decimal price = 10m)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock, price);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return (harness, result.Value.PaymentAttemptId, result.Value.OrderId);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_SuccessfulTransaction_CapturesPaymentAndDeliversCode()
    {
        var (harness, attemptId, orderId) = await InitiateAsync(stock: 5, price: 10m);
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Succeeded, result.Outcome);

        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal("DotFawry", order.PaymentProvider);

        // Pre-existing OrderFulfillmentService behavior (see OrderFulfillmentServiceTests, and the
        // identical note in DotPaymentVerificationServiceTests): a single-call full delivery is
        // left Processing, not Completed — the enqueued RetryOrderFulfillment job picks it up.
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Contains(OperationalJobTypes.RetryOrderFulfillment, harness.JobQueue.EnqueuedJobTypes);

        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
        Assert.Single(harness.Communication.SentRequests);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
    }

    [Theory]
    [InlineData("1004", "Insufficient balance")]
    [InlineData("1005", "Other billing error")]
    [InlineData("1011", "Invalid MSISDN")]
    [InlineData("1013", "The user is not subscribed")]
    public async Task VerifyAndFinalizeAsync_UnsuccessfulTransaction_MarksOrderFailedAndDeliversNothing(
        string billingResultCode, string billingResultDesc)
    {
        var (harness, attemptId, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new("0", "found", billingResultCode, billingResultDesc, "dot-trans-1");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Failed, result.Outcome);

        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(PaymentStatus.Failed, order.PaymentStatus);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
        Assert.Empty(harness.Communication.SentRequests);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_StillProcessingAtCheckStatusLayer_ReleasesBackToPending()
    {
        var (harness, attemptId, orderId) = await InitiateAsync();
        // Some operators keep reporting "1000: still processing" from Check Transaction Status too
        // — must never be treated as failure or success.
        harness.Gateway.StatusResult = new("0", "found", "1000", "The billing transaction is still being processed.", "dot-trans-1");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.StillPending, result.Outcome);
        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_CalledTwiceAfterSuccess_IsIdempotentAndDoesNotDoubleFulfillOrNotify()
    {
        var (harness, attemptId, _) = await InitiateAsync(stock: 5, price: 10m);
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");

        var first = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);
        var second = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Succeeded, first.Outcome);
        Assert.Equal(DotFawryVerificationOutcome.Succeeded, second.Outcome);

        // Only the first call actually talked to DOT; the second replayed the cached terminal state
        // — this is what makes DOT's notification-retry contract ("resend until we return 1") safe.
        Assert.Single(harness.Gateway.StatusCheckCalls);
        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
        Assert.Single(harness.Communication.SentRequests);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_AlreadyBeingVerifiedConcurrently_ShortCircuitsWithoutCallingProviderAgain()
    {
        var (harness, attemptId, _) = await InitiateAsync();

        var claimant = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        claimant.BeginVerification();
        await harness.CommerceDb.SaveChangesAsync(CancellationToken.None);

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.StillPending, result.Outcome);
        Assert.Empty(harness.Gateway.StatusCheckCalls);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_ProviderTimeout_ReleasesClaimAndLeavesOrderPending()
    {
        var (harness, attemptId, orderId) = await InitiateAsync();
        harness.Gateway.FailStatusCheck = true;

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.StillPending, result.Outcome);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
    }

    [Fact]
    public async Task VerifyAndFinalizeAsync_UnknownPaymentAttempt_ReturnsNotFound()
    {
        var harness = DotFawryTestHarness.Create();

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task NotificationHandler_UnknownPartnerTransId_AcksWithoutThrowing()
    {
        var harness = DotFawryTestHarness.Create();

        var result = await harness.NotificationHandler.Handle(
            new HandleDotFawryNotificationCommand("dot-trans-x", "unknown-partner-txid", 141, "201001234567", "10", "1", "1000", "processing"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task NotificationHandler_KnownAttempt_TriggersVerificationAndResolvesSuccess()
    {
        var (harness, attemptId, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");
        var partnerTxId = (await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId)).PartnerTxId;

        var result = await harness.NotificationHandler.Handle(
            new HandleDotFawryNotificationCommand("dot-trans-1", partnerTxId, 141, "201001234567", "10", "1", "0", "successfully charged"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }
}
