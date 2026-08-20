using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Dot;

public sealed class DotCallbackAndNotificationTests
{
    private static async Task<(DotTestHarness Harness, string PartnerTxId, Guid PaymentAttemptId, Guid OrderId)> InitiateAsync()
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US"), CancellationToken.None);
        Assert.True(result.IsSuccess);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == result.Value.PaymentAttemptId);
        return (harness, attempt.PartnerTxId, attempt.Id, result.Value.OrderId);
    }

    [Fact]
    public async Task Callback_ValidReasonCodeZero_TriggersVerificationAndCapturesPayment()
    {
        var (harness, partnerTxId, attemptId, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        var result = await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand(partnerTxId, "dot-txid-1", 21, "1", "923000000000", "0", "ok"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(attemptId, result.Value);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task Callback_SpoofedReasonCodeZeroButProviderDisagrees_NeverCompletesOrder()
    {
        var (harness, partnerTxId, _, orderId) = await InitiateAsync();
        // Attacker/browser claims success, but DOT's own authoritative status check disagrees.
        harness.Gateway.StatusResult = new(1001, "Invalid PIN entered by user", null, null, null);

        var result = await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand(partnerTxId, "dot-txid-1", 21, "1", "923000000000", "0", "spoofed success"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.NotEqual(OrderStatus.Completed, order.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
    }

    [Fact]
    public async Task Callback_UnknownPartnerTxId_ReturnsInvalidAndTouchesNothing()
    {
        var harness = DotTestHarness.Create();

        var result = await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand("does-not-exist", null, 21, "1", null, "0", "ok"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotCallbackInvalid.Code, result.Error.Code);
        Assert.Empty(harness.Gateway.StatusCheckCalls);
    }

    [Fact]
    public async Task Callback_MismatchedOperatorContext_ReturnsInvalidAndNeverVerifies()
    {
        var (harness, partnerTxId, _, orderId) = await InitiateAsync();

        var result = await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand(partnerTxId, "dot-txid-1", 999, "1", null, "0", "ok"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(harness.Gateway.StatusCheckCalls);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task Callback_ReplayAfterAlreadySucceeded_IsIdempotent()
    {
        var (harness, partnerTxId, _, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand(partnerTxId, "dot-txid-1", 21, "1", "923000000000", "0", "ok"),
            CancellationToken.None);
        await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand(partnerTxId, "dot-txid-1", 21, "1", "923000000000", "0", "ok"),
            CancellationToken.None);

        Assert.Single(harness.Gateway.StatusCheckCalls);
        Assert.Single(harness.Communication.SentRequests);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task Notification_ValidPayload_TriggersVerificationAndCapturesPayment()
    {
        var (harness, partnerTxId, _, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        var result = await harness.NotificationHandler.Handle(
            new HandleDotNotificationCommand("dot-txid-1", partnerTxId, 21, "923000000000", "10.0", "1", "0", "successfully charged"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task Notification_UnknownPartnerTxId_AcksSuccessWithoutCreatingOrMutatingAnything()
    {
        var harness = DotTestHarness.Create();

        var result = await harness.NotificationHandler.Handle(
            new HandleDotNotificationCommand("dot-txid-1", "does-not-exist", 21, "923000000000", "10.0", "1", "0", "successfully charged"),
            CancellationToken.None);

        // Still acked (DOT must not retry forever on an unresolvable transaction), but nothing happened.
        Assert.True(result.IsSuccess);
        Assert.Empty(harness.CommerceDb.PaymentAttempts);
        Assert.Empty(harness.Gateway.StatusCheckCalls);
    }

    [Fact]
    public async Task Notification_DuplicateDelivery_IsIdempotentAndDoesNotDoubleFulfill()
    {
        var (harness, partnerTxId, _, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        var notification = new HandleDotNotificationCommand(
            "dot-txid-1", partnerTxId, 21, "923000000000", "10.0", "1", "0", "successfully charged");

        await harness.NotificationHandler.Handle(notification, CancellationToken.None);
        await harness.NotificationHandler.Handle(notification, CancellationToken.None);
        await harness.NotificationHandler.Handle(notification, CancellationToken.None);

        Assert.Single(harness.Gateway.StatusCheckCalls);
        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
        Assert.Single(harness.Communication.SentRequests);
    }

    [Fact]
    public async Task CallbackThenNotification_ForSameAttempt_ResolvesOnceAndIsConsistent()
    {
        var (harness, partnerTxId, _, orderId) = await InitiateAsync();
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");

        await harness.CallbackHandler.Handle(
            new HandleDotRedirectCallbackCommand(partnerTxId, "dot-txid-1", 21, "1", "923000000000", "0", "ok"),
            CancellationToken.None);
        await harness.NotificationHandler.Handle(
            new HandleDotNotificationCommand("dot-txid-1", partnerTxId, 21, "923000000000", "10.0", "1", "0", "successfully charged"),
            CancellationToken.None);

        Assert.Single(harness.Gateway.StatusCheckCalls);
        Assert.Single(harness.Communication.SentRequests);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }
}
