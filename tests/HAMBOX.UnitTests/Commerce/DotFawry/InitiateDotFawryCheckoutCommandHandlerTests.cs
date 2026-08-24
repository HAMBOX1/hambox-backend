using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.UnitTests.Commerce.DotFawry.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.DotFawry;

public sealed class InitiateDotFawryCheckoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_ProcessingResult_CreatesPendingOrderAndPaymentAttemptAndReturnsFawryReference()
    {
        var harness = DotFawryTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", "Buyer Name"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("9638322189", result.Value.FawryReferenceNumber);

        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == result.Value.OrderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal("dot-fawry", order.PaymentMethod);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.OrderId == order.Id);
        Assert.Equal("DotFawry", attempt.Provider);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Equal(10m, attempt.ExpectedAmount);
        Assert.Equal("USD", attempt.ExpectedCurrency);
        Assert.Equal("141", attempt.OperatorId);
        Assert.Equal("9638322189", attempt.ProviderReferenceCode);
        Assert.EndsWith("4567", attempt.MaskedMsisdn); // last 4 digits only, never the full number

        // Nothing was reserved or committed yet — payment isn't confirmed. resultCode 1000 never
        // triggers fulfillment.
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
        Assert.Equal(5, harness.InventoryEngine.AvailableStockByVariant[variant.Id]);

        Assert.Single(harness.Gateway.ChargeCalls);
        var chargeCall = harness.Gateway.ChargeCalls[0];
        Assert.Equal("buyer@example.com", chargeCall.CustomerEmail);
        Assert.Equal("Buyer Name", chargeCall.CustomerName);
        Assert.Equal("201001234567", chargeCall.Msisdn);
        Assert.Single(chargeCall.Items);
        Assert.Equal(1, chargeCall.Items[0].Quantity);
        Assert.Equal(10m, chargeCall.Items[0].Price);
    }

    [Fact]
    public async Task Handle_ImmediateSuccessResult_FinalizesThroughSingleVerificationPath()
    {
        var harness = DotFawryTestHarness.Create();
        harness.Gateway.ChargeResult = new("0", "successfully charged", "dot-trans-1", null);
        harness.Gateway.StatusResult = new("0", "found", "0", "Successfully charged", "dot-trans-1");
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.FawryReferenceNumber);

        // Never trusted from the initiate response alone — must have gone through
        // DotFawryPaymentVerificationService, which re-verified via Check Transaction Status.
        Assert.Single(harness.Gateway.StatusCheckCalls);
        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == result.Value.OrderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task Handle_PricingNotConfigured_ReturnsErrorAndCreatesNoRecords()
    {
        var harness = DotFawryTestHarness.Create();
        harness.ChargeAmountResolver.PassThroughUsd = false;
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotFawryPricingNotConfigured.Code, result.Error.Code);
        Assert.Empty(harness.CommerceDb.Orders);
        Assert.Empty(harness.CommerceDb.PaymentAttempts);
        Assert.Empty(harness.Gateway.ChargeCalls);
    }

    [Fact]
    public async Task Handle_MissingCredentials_ReturnsGatewayMisconfiguredAndCreatesNoRecords()
    {
        var incompleteSettings = new HAMBOX.Modules.Commerce.Application.Options.DotFawrySettings
        {
            BaseUrl = "https://dot-jo.biz",
            PartnerId = string.Empty,
            ServiceId = "1",
            OperatorId = "141",
            Username = "test-user",
            Password = "test-pass",
        };
        var harness = DotFawryTestHarness.Create(settingsOverride: incompleteSettings);
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotFawryGatewayMisconfigured.Code, result.Error.Code);
        Assert.Empty(harness.CommerceDb.Orders);
    }

    [Fact]
    public async Task Handle_ChargeRequestFails_LeavesOrderPendingForReconciliationSweep()
    {
        var harness = DotFawryTestHarness.Create();
        harness.Gateway.FailCharge = true;
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotFawryProviderUnavailable.Code, result.Error.Code);

        var order = await harness.CommerceDb.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Pending, order.Status);
        var attempt = await harness.CommerceDb.PaymentAttempts.SingleAsync();
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ReturnsAuthenticationRequired()
    {
        var harness = DotFawryTestHarness.Create(userId: null);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.AuthenticationRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_EmptyCart_ReturnsCartEmpty()
    {
        var harness = DotFawryTestHarness.Create();

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotFawryCheckoutCommand("buyer@example.com", "EG", "201001234567", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.CartEmpty.Code, result.Error.Code);
    }
}
