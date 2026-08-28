using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.UnitTests.Commerce.DotFawry.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.DotFawry;

/// <summary>
/// Covers the Egyptian mobile wallet extension to DOT Fawry Direct Billing: Orange Cash (opId 117)
/// and Vodafone Cash (opId 114) alongside the original Fawry (opId 141), sharing every credential
/// and code path — only <c>opId</c> varies. See <c>DotFawryWalletOperator</c> for the single source
/// of truth mapping wallet -&gt; opId.
/// </summary>
public sealed class DotFawryWalletOperatorTests
{
    [Theory]
    [InlineData("Fawry", "141")]
    [InlineData("OrangeCash", "117")]
    [InlineData("VodafoneCash", "114")]
    public async Task Handle_SelectedWallet_SendsAndStoresTheCorrectOpId(string wallet, string expectedOpId)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, wallet, "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(wallet, result.Value.Operator);

        // Stored on the PaymentAttempt — available to verification, reconciliation, webhook
        // processing, and audit/history, all of which read it back off this same entity.
        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.OrderId == result.Value.OrderId);
        Assert.Equal(expectedOpId, attempt.OperatorId);

        // The Direct Billing request itself carried the correct opId — never hardcoded 141.
        Assert.Single(harness.Gateway.ChargeCalls);
        Assert.Equal(expectedOpId, harness.Gateway.ChargeCalls[0].OperatorId);
    }

    [Theory]
    [InlineData("Fawry")]
    [InlineData("OrangeCash")]
    [InlineData("VodafoneCash")]
    public async Task Handle_SelectedWallet_CustomerStatusReportsTheSameWallet(string wallet)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, wallet, "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);
        Assert.True(initiation.IsSuccess);

        var status = await harness.StatusQueryHandler.Handle(
            new GetDotFawryPaymentStatusQuery(initiation.Value.PaymentAttemptId), CancellationToken.None);

        Assert.True(status.IsSuccess);
        Assert.Equal(wallet, status.Value.Operator);
    }

    [Theory]
    [InlineData("orangecash")]
    [InlineData("VODAFONECASH")]
    [InlineData("fawry")]
    public async Task Handle_WalletNameIsCaseInsensitive_Succeeds(string wallet)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, wallet, "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Validator_UnknownWallet_FailsValidation()
    {
        var validator = new InitiateDotFawryCheckoutCommandValidator();
        var command = new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, "Etisalat", "127.0.0.1", "test-agent", "en");

        var validation = await validator.ValidateAsync(command);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.PropertyName == nameof(InitiateDotFawryCheckoutCommand.Wallet));
    }

    [Theory]
    [InlineData("Fawry", "141")]
    [InlineData("OrangeCash", "117")]
    [InlineData("VodafoneCash", "114")]
    public async Task VerifyAndFinalizeAsync_UsesTheAttemptsOwnOperatorId_NotAGlobalDefault(string wallet, string expectedOpId)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, wallet, "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);
        Assert.True(initiation.IsSuccess);

        // resultCode 1000 (the fixture default) leaves the attempt Pending — trigger a fresh
        // verification pass exactly like the reconciliation sweep or a webhook would.
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");
        var result = await harness.VerificationService.VerifyAndFinalizeAsync(
            initiation.Value.PaymentAttemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Succeeded, result.Outcome);
        Assert.Contains(expectedOpId, harness.Gateway.StatusCheckOperatorIds);
    }

    [Theory]
    [InlineData(141, "Fawry")]
    [InlineData(117, "OrangeCash")]
    [InlineData(114, "VodafoneCash")]
    public async Task NotificationHandler_WebhookForEachOperator_ResolvesTheMatchingAttemptToSuccess(int opId, string wallet)
    {
        var (harness, attemptId, orderId) = await InitiateAsync(wallet);
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");
        var partnerTxId = (await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId)).PartnerTxId;

        // The webhook payload carries whatever opId DOT reports back — the handler locates the
        // attempt by partnerTransId alone (unique per attempt) and re-verifies server-to-server;
        // it is never expected to special-case opId itself.
        var result = await harness.NotificationHandler.Handle(
            new HandleDotFawryNotificationCommand("dot-trans-1", partnerTxId, opId, "201001234567", "10", "1", "0", "successfully charged"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Theory]
    [InlineData("Fawry")]
    [InlineData("OrangeCash")]
    [InlineData("VodafoneCash")]
    public async Task VerifyAndFinalizeAsync_ResultCode1000_NeverTreatedAsSuccessForAnyWallet(string wallet)
    {
        var (harness, attemptId, orderId) = await InitiateAsync(wallet);
        // "1000: the transaction is being processed" — the documented, normal Direct Billing
        // response for an asynchronous operator. Must never finalize the order.
        harness.Gateway.StatusResult = new("0", "found", "1000", "The billing transaction is still being processed.", "dot-trans-1");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.StillPending, result.Outcome);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
    }

    [Theory]
    [InlineData("OrangeCash")]
    [InlineData("VodafoneCash")]
    public async Task VerifyAndFinalizeAsync_FailedTransaction_MarksOrderFailedForNonFawryWallets(string wallet)
    {
        var (harness, attemptId, orderId) = await InitiateAsync(wallet);
        harness.Gateway.StatusResult = new("0", "found", "1004", "Insufficient balance", "dot-trans-1");

        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Failed, result.Outcome);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
    }

    [Theory]
    [InlineData("OrangeCash")]
    [InlineData("VodafoneCash")]
    public async Task VerifyAndFinalizeAsync_CalledTwiceAfterSuccess_IsIdempotentForNonFawryWallets(string wallet)
    {
        var (harness, attemptId, _) = await InitiateAsync(wallet);
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");

        var first = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);
        var second = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Succeeded, first.Outcome);
        Assert.Equal(DotFawryVerificationOutcome.Succeeded, second.Outcome);

        // Only the first call talked to DOT — the duplicate webhook/reconciliation-race contract.
        Assert.Single(harness.Gateway.StatusCheckCalls);
        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
    }

    [Theory]
    [InlineData("OrangeCash")]
    [InlineData("VodafoneCash")]
    public async Task VerifyAndFinalizeAsync_ProcessingThenSuccess_FulfillsOnlyAfterConfirmedSuccess(string wallet)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        // Charge itself returns "1000 processing" — the expected asynchronous path.
        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, wallet, "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);
        Assert.True(initiation.IsSuccess);

        var attemptId = initiation.Value.PaymentAttemptId;
        var attemptAfterCharge = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.Id == attemptId);
        Assert.Equal(PaymentAttemptStatus.Pending, attemptAfterCharge.Status);
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);

        // Only once Check Transaction Status confirms success does fulfillment ever run.
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");
        var result = await harness.VerificationService.VerifyAndFinalizeAsync(attemptId, CancellationToken.None);

        Assert.Equal(DotFawryVerificationOutcome.Succeeded, result.Outcome);
        Assert.Single(harness.CommerceDb.OrderLicenseKeys);
    }

    private static async Task<(DotFawryTestHarness Harness, Guid PaymentAttemptId, Guid OrderId)> InitiateAsync(string wallet)
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null, wallet, "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return (harness, result.Value.PaymentAttemptId, result.Value.OrderId);
    }
}
