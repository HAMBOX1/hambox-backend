using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Audit R30: the Cryptomus sweep finalises a paid invoice whose webhook was lost, leaves a customer who is still
/// paying on-chain alone (it must never expire an attempt at the local window), and applies the provider's own
/// terminal failures.
/// </summary>
public sealed class CryptoPaymentReconciliationTests
{
    private sealed class ScriptedCryptomusGateway : ICryptomusPaymentGateway
    {
        public string Status { get; set; } = "paid";
        public bool IsFinal { get; set; } = true;
        public decimal? PaidUsd { get; set; }
        public int InfoCalls { get; private set; }

        public Task<Result<CryptomusInvoiceResult>> CreateInvoiceAsync(
            CryptomusCreateInvoiceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<CryptomusInvoiceResult>> GetPaymentInfoByOrderIdAsync(
            string orderId, CancellationToken cancellationToken = default)
        {
            InfoCalls++;
            return Task.FromResult(Result.Success(new CryptomusInvoiceResult(
                "uuid-1", orderId, null, Status, IsFinal, PaidUsd, "USDT")));
        }
    }

    private sealed class Rig
    {
        public required CommerceDbContext CommerceDb { get; init; }
        public required ScriptedCryptomusGateway Gateway { get; init; }
        public required FakeInventoryEngine Inventory { get; init; }
        public required ReconcileCryptomusPaymentsJobHandler Handler { get; init; }
        public required Order Order { get; init; }
        public required PaymentAttempt Attempt { get; init; }
    }

    private static async Task<Rig> CreateAsync(DateTimeOffset attemptExpiresOnUtc, decimal amount = 25m)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "buyer@example.com", "US", "cryptomus",
            amount, 0m, 0m, amount,
            [(productId, "Gift card", 1, amount, (Guid?)variantId, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        var attempt = PaymentAttempt.CreatePendingCryptomus(
            order.Id, $"partner-{Guid.NewGuid():N}", amount, "USD", attemptExpiresOnUtc, null);

        commerceDb.Orders.Add(order);
        commerceDb.PaymentAttempts.Add(attempt);
        await commerceDb.SaveChangesAsync();

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variantId] = 1;
        inventory.CodesByVariant[variantId] = new Queue<string>(["CRYPTO-PAID-KEY-1"]);

        var gateway = new ScriptedCryptomusGateway { PaidUsd = amount };
        var communication = new FakeCommunicationService();
        var fulfillment = new OrderFulfillmentService(
            commerceDb, inventory, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(),
            HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new FakeDeliveryTicketService(),
            NullLogger<OrderFulfillmentService>.Instance);
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb, new FakeDotPlatformSettingsProvider(),
            new ReferralRewardService(commerceDb, new FakeMembershipEngine()),
            communication, NullLogger<ReferralLifecycleService>.Instance);

        var verification = new CryptomusPaymentVerificationService(
            commerceDb, catalogDb, new FakeCommerceTransactionService(), gateway, fulfillment,
            new PromotionRedemptionService(commerceDb), referralLifecycle, communication,
            new FakeOperationalJobQueue(), NullLogger<CryptomusPaymentVerificationService>.Instance);

        var handler = new ReconcileCryptomusPaymentsJobHandler(
            serializer: null!, commerceDb, verification, NullLogger<ReconcileCryptomusPaymentsJobHandler>.Instance);

        return new Rig
        {
            CommerceDb = commerceDb,
            Gateway = gateway,
            Inventory = inventory,
            Handler = handler,
            Order = order,
            Attempt = attempt,
        };
    }

    private static Task RunAsync(Rig rig) =>
        rig.Handler.HandleAsync(null, null!, CancellationToken.None);

    [Fact]
    public async Task PaidInvoice_WithALostWebhook_IsFinalisedByTheSweep()
    {
        var rig = await CreateAsync(DateTimeOffset.UtcNow.AddMinutes(20));

        await RunAsync(rig);

        await rig.CommerceDb.Entry(rig.Attempt).ReloadAsync();
        await rig.CommerceDb.Entry(rig.Order).ReloadAsync();
        Assert.Equal(PaymentAttemptStatus.Succeeded, rig.Attempt.Status);
        Assert.Equal(PaymentStatus.Paid, rig.Order.PaymentStatus);
        Assert.Equal(OrderStatus.Completed, rig.Order.Status);
        Assert.Equal("CRYPTO-PAID-KEY-1", (await rig.CommerceDb.OrderLicenseKeys.SingleAsync()).LicenseKey);
    }

    [Fact]
    public async Task InvoiceStillBeingPaid_PastTheLocalWindow_IsNotExpired()
    {
        // The window ended two hours ago, but the customer's on-chain payment is still confirming.
        var rig = await CreateAsync(DateTimeOffset.UtcNow.AddHours(-2));
        rig.Gateway.Status = "check";
        rig.Gateway.IsFinal = false;

        await RunAsync(rig);

        await rig.CommerceDb.Entry(rig.Attempt).ReloadAsync();
        Assert.Equal(PaymentAttemptStatus.Pending, rig.Attempt.Status);
        Assert.Equal(1, rig.Gateway.InfoCalls);
    }

    [Fact]
    public async Task ProviderCancellation_FailsTheAttemptAndTheOrder_AndReleasesTheHeldStock()
    {
        var rig = await CreateAsync(DateTimeOffset.UtcNow.AddMinutes(5));
        rig.Gateway.Status = "cancel";
        rig.Gateway.IsFinal = true;

        await RunAsync(rig);

        await rig.CommerceDb.Entry(rig.Attempt).ReloadAsync();
        await rig.CommerceDb.Entry(rig.Order).ReloadAsync();
        Assert.Equal(PaymentAttemptStatus.Failed, rig.Attempt.Status);
        Assert.Equal(OrderStatus.Failed, rig.Order.Status);
        Assert.Empty(rig.CommerceDb.OrderLicenseKeys);
    }

    [Fact]
    public async Task AnAttemptUnresolvedADayAfterItsWindow_IsFinallyExpired()
    {
        var rig = await CreateAsync(DateTimeOffset.UtcNow.AddHours(-30));
        rig.Gateway.Status = "process";
        rig.Gateway.IsFinal = false;

        await RunAsync(rig);

        await rig.CommerceDb.Entry(rig.Attempt).ReloadAsync();
        Assert.Equal(PaymentAttemptStatus.Expired, rig.Attempt.Status);
    }

    [Fact]
    public async Task AttemptsOfOtherGateways_AreLeftAlone()
    {
        var rig = await CreateAsync(DateTimeOffset.UtcNow.AddMinutes(20));
        var dot = PaymentAttempt.CreatePendingDot(
            Guid.NewGuid(), "dot-partner", "117", "1", 10m, "EGP", DateTimeOffset.UtcNow.AddHours(-30), null);
        rig.CommerceDb.PaymentAttempts.Add(dot);
        await rig.CommerceDb.SaveChangesAsync();

        await RunAsync(rig);

        await rig.CommerceDb.Entry(dot).ReloadAsync();
        Assert.Equal(PaymentAttemptStatus.Pending, dot.Status);
    }
}
