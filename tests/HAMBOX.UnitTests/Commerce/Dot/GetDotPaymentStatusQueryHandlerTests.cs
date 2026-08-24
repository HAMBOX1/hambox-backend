using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Dot;

public sealed class GetDotPaymentStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_OwnPendingAttempt_ReturnsPendingStatus()
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);
        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash"), CancellationToken.None);

        var result = await harness.StatusQueryHandler.Handle(
            new GetDotPaymentStatusQuery(initiation.Value.PaymentAttemptId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Null(result.Value.CompletedOrderId);
    }

    [Fact]
    public async Task Handle_AnotherUsersAttempt_ReturnsNotFoundRatherThanLeakingStatus()
    {
        var harness = DotTestHarness.Create(userId: "user-1");
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);
        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash"), CancellationToken.None);

        var attackerCurrentUser = new FakeCurrentUserService("attacker");
        var attackerStatusHandler = new GetDotPaymentStatusQueryHandler(harness.CommerceDb, attackerCurrentUser);

        var result = await attackerStatusHandler.Handle(
            new GetDotPaymentStatusQuery(initiation.Value.PaymentAttemptId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotPaymentAttemptNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_UnknownAttemptId_ReturnsNotFound()
    {
        var harness = DotTestHarness.Create();

        var result = await harness.StatusQueryHandler.Handle(
            new GetDotPaymentStatusQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotPaymentAttemptNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_SucceededAttempt_ReturnsCompletedOrderId()
    {
        var harness = DotTestHarness.Create();
        var (product, variant) = await harness.SeedProductAsync(stock: 5, price: 10m);
        await harness.SeedCartAsync(product, variant);
        var initiation = await harness.InitiateHandler.Handle(
            new InitiateDotCheckoutCommand("buyer@example.com", "US", "OrangeCash"), CancellationToken.None);
        harness.Gateway.StatusResult = new(0, "successful transaction", DateTimeOffset.UtcNow, 10m, "USD");
        await harness.VerificationService.VerifyAndFinalizeAsync(initiation.Value.PaymentAttemptId, CancellationToken.None);

        var result = await harness.StatusQueryHandler.Handle(
            new GetDotPaymentStatusQuery(initiation.Value.PaymentAttemptId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value.Status);
        Assert.Equal(initiation.Value.OrderId, result.Value.CompletedOrderId);
    }
}
