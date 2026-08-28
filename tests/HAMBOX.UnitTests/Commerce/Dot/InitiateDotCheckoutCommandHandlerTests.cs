using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Dot;

public sealed class InitiateDotCheckoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCart_CreatesPendingOrderAndPaymentAttemptAndReturnsRedirectUrl()
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("otp-lp", result.Value.OtpLandingPageUrl);
        Assert.Contains("token=fake-token", result.Value.OtpLandingPageUrl);

        var order = await harness.CommerceDb.Orders.FirstAsync(o => o.Id == result.Value.OrderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal("dot", order.PaymentMethod);

        var attempt = await harness.CommerceDb.PaymentAttempts.FirstAsync(p => p.OrderId == order.Id);
        Assert.Equal(10m, attempt.ExpectedAmount);
        Assert.Equal("USD", attempt.ExpectedCurrency);
        Assert.Equal("117", attempt.OperatorId);
        Assert.Equal("1", attempt.ServiceId);

        // Nothing was reserved or committed yet — payment isn't confirmed.
        Assert.Empty(harness.CommerceDb.OrderLicenseKeys);
        Assert.Equal(5, harness.InventoryEngine.AvailableStockByVariant[variant.Id]);

        Assert.Single(harness.Gateway.AccessTokenCalls);
    }

    [Fact]
    public async Task Handle_PricingNotConfigured_ReturnsErrorAndCreatesNoRecords()
    {
        var harness = DotTestHarness.Create();
        harness.PriceResolver.PassThroughUsd = false;
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotPricingNotConfigured.Code, result.Error.Code);
        Assert.Empty(harness.CommerceDb.Orders);
        Assert.Empty(harness.CommerceDb.PaymentAttempts);
        Assert.Empty(harness.Gateway.AccessTokenCalls);
    }

    [Fact]
    public async Task Handle_MissingCredentials_ReturnsGatewayMisconfiguredAndCreatesNoRecords()
    {
        var incompleteSettings = new HAMBOX.Modules.Commerce.Application.Options.DotSettings
        {
            BaseUrl = "https://dot-jo.biz",
            PartnerId = string.Empty,
            ServiceId = "1",
            Username = "test-user",
            Password = "test-pass",
            PublicRedirectUrl = "https://hambox.test/api/payments/dot/callback",
            FrontendResultUrl = "https://hambox.test/checkout/dot/result",
        };
        var harness = DotTestHarness.Create(settingsOverride: incompleteSettings);
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotGatewayMisconfigured.Code, result.Error.Code);
        Assert.Empty(harness.CommerceDb.Orders);
    }

    [Fact]
    public async Task Handle_AccessTokenRequestFails_LeavesOrderPendingForExpirySweep()
    {
        var harness = DotTestHarness.Create();
        harness.Gateway.FailAccessToken = true;
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotProviderUnavailable.Code, result.Error.Code);

        // The order/attempt are already persisted (durability-before-network-call) but nothing
        // was charged — the reconciliation sweep will expire it if the customer never retries.
        var order = await harness.CommerceDb.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Pending, order.Status);
        var attempt = await harness.CommerceDb.PaymentAttempts.SingleAsync();
        Assert.Equal(HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Pending, attempt.Status);
    }

    [Fact]
    public async Task Handle_AccessTokenResultCodeNonZero_ReturnsProviderUnavailable()
    {
        var harness = DotTestHarness.Create();
        harness.Gateway.AccessTokenResult = new(1009, "Internal error", null);
        var (product, variant) = await harness.SeedProductAsync(stock: 5);
        await harness.SeedCartAsync(product, variant);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ReturnsAuthenticationRequired()
    {
        var harness = DotTestHarness.Create(userId: null);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.AuthenticationRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_EmptyCart_ReturnsCartEmpty()
    {
        var harness = DotTestHarness.Create();

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.CartEmpty.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_InsufficientInventory_ReturnsErrorAndCreatesNoRecords()
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 1);
        await harness.SeedCartAsync(product, variant, quantity: 5);

        var result = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(harness.CommerceDb.Orders);
    }
}
