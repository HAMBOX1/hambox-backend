using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Security;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Account.Library;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Library;

/// <summary>
/// The customer OTP gate in front of "show my license key" (audit R9): Off / FirstReveal / EveryReveal, wrong and
/// missing codes, a key that is not the caller's own, and that nothing is revealed (or audited) unless the gate passes.
/// </summary>
public sealed class RevealLicenseKeyOtpTests
{
    private const string User = "user-1";
    private const string Plaintext = "REAL-SECRET-CODE-XYZ-0001";

    private static async Task<(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Db,
        OrderLicenseKey Key)> SeedAsync(string owner = User)
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var order = Order.Create(
            userId: owner,
            orderNumber: $"ORD-{Guid.NewGuid():N}",
            email: "buyer@example.com",
            country: "US",
            paymentMethod: "development",
            subtotal: 19.99m,
            discountAmount: 0m,
            taxAmount: 0m,
            totalAmount: 19.99m,
            items: [(productId, "Test Product", 1, 19.99m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        order.Complete();

        var key = OrderLicenseKey.Create(order.Id, order.Items.Single().Id, productId, Plaintext);
        db.Orders.Add(order);
        db.OrderLicenseKeys.Add(key);
        await db.SaveChangesAsync();
        return (db, key);
    }

    private static (RevealCustomerLibraryKeyQueryHandler Handler, FakeCustomerActionOtpService Otp) Handler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db, string mode, string user = User)
    {
        var otp = new FakeCustomerActionOtpService();
        var settings = new FakeCommerceSettingsProvider
        {
            Security = new SecuritySettingsPayload(5, 15, false, CustomerRevealOtpMode: mode),
        };
        return (new RevealCustomerLibraryKeyQueryHandler(db, new FakeCurrentUserService(user), settings, otp), otp);
    }

    [Fact]
    public async Task Off_RevealsWithoutAnyCode()
    {
        var (db, key) = await SeedAsync();
        var (handler, otp) = Handler(db, "Off");

        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Plaintext, result.Value.LicenseKey);
        Assert.Empty(otp.Verified);
    }

    [Fact]
    public async Task FirstReveal_WithoutACode_IsRefused_AndNothingIsAudited()
    {
        var (db, key) = await SeedAsync();
        var (handler, _) = Handler(db, "FirstReveal");

        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommerceErrors.RevealOtpRequired.Code, result.Error.Code);
        Assert.Empty(db.OrderAuditEntries);
    }

    [Fact]
    public async Task FirstReveal_WrongCode_IsRefused()
    {
        var (db, key) = await SeedAsync();
        var (handler, _) = Handler(db, "FirstReveal");

        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id, "000000"), CancellationToken.None);

        Assert.Equal(CommerceErrors.RevealOtpInvalid.Code, result.Error.Code);
        Assert.Empty(db.OrderAuditEntries);
    }

    [Fact]
    public async Task FirstReveal_CorrectCode_Reveals_ThenTheSameKeyNoLongerAsksForOne()
    {
        var (db, key) = await SeedAsync();
        var (handler, otp) = Handler(db, "FirstReveal");

        var first = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id, "123456"), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(Plaintext, first.Value.LicenseKey);
        Assert.Single(otp.Verified);
        Assert.Equal(key.Id.ToString(), otp.Verified[0].ScopeKey);

        var again = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id), CancellationToken.None);
        Assert.True(again.IsSuccess);
        Assert.Single(otp.Verified);
    }

    [Fact]
    public async Task EveryReveal_AsksForAFreshCode_EachTime()
    {
        var (db, key) = await SeedAsync();
        var (handler, _) = Handler(db, "EveryReveal");

        var first = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id, "123456"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id), CancellationToken.None);
        Assert.Equal(CommerceErrors.RevealOtpRequired.Code, second.Error.Code);
    }

    [Theory]
    [InlineData(CustomerActionOtpVerifyStatus.Expired, "Commerce.RevealOtpExpired")]
    [InlineData(CustomerActionOtpVerifyStatus.Locked, "Commerce.RevealOtpLocked")]
    [InlineData(CustomerActionOtpVerifyStatus.NotRequested, "Commerce.RevealOtpRequired")]
    public async Task VerificationFailures_MapToTheirOwnErrors(CustomerActionOtpVerifyStatus status, string expectedCode)
    {
        var (db, key) = await SeedAsync();
        var (handler, otp) = Handler(db, "FirstReveal");
        otp.ForcedVerifyStatus = status;

        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id, "123456"), CancellationToken.None);

        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public async Task AnotherCustomersKey_IsNotFound_NeverReachesTheOtpCheck()
    {
        var (db, key) = await SeedAsync(owner: "owner");
        var (handler, otp) = Handler(db, "FirstReveal", user: "intruder");

        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id, "123456"), CancellationToken.None);

        Assert.Equal(CommerceErrors.OrderLicenseKeyNotFound.Code, result.Error.Code);
        Assert.Empty(otp.Verified);
    }

    [Fact]
    public async Task RequestingACode_ForAnotherCustomersKey_IsNotFound_AndSendsNothing()
    {
        var (db, key) = await SeedAsync(owner: "owner");
        var otp = new FakeCustomerActionOtpService();
        var handler = new RequestLicenseKeyRevealOtpCommandHandler(db, new FakeCurrentUserService("intruder"), otp);

        var result = await handler.Handle(new RequestLicenseKeyRevealOtpCommand(key.Id), CancellationToken.None);

        Assert.Equal(CommerceErrors.OrderLicenseKeyNotFound.Code, result.Error.Code);
        Assert.Empty(otp.Issued);
    }

    [Fact]
    public async Task RequestingACode_ForYourOwnKey_IssuesOneBoundToThatKey()
    {
        var (db, key) = await SeedAsync();
        var otp = new FakeCustomerActionOtpService();
        var handler = new RequestLicenseKeyRevealOtpCommandHandler(db, new FakeCurrentUserService(User), otp);

        var result = await handler.Handle(new RequestLicenseKeyRevealOtpCommand(key.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var issued = Assert.Single(otp.Issued);
        Assert.Equal(key.Id.ToString(), issued.ScopeKey);
    }

    [Theory]
    [InlineData(CustomerActionOtpIssueStatus.CooldownActive, "Commerce.RevealOtpCooldown")]
    [InlineData(CustomerActionOtpIssueStatus.Locked, "Commerce.RevealOtpLocked")]
    [InlineData(CustomerActionOtpIssueStatus.DeliveryFailed, "Commerce.RevealOtpDeliveryFailed")]
    public async Task IssueFailures_MapToTheirOwnErrors(CustomerActionOtpIssueStatus status, string expectedCode)
    {
        var (db, key) = await SeedAsync();
        var otp = new FakeCustomerActionOtpService { IssueStatus = status };
        var handler = new RequestLicenseKeyRevealOtpCommandHandler(db, new FakeCurrentUserService(User), otp);

        var result = await handler.Handle(new RequestLicenseKeyRevealOtpCommand(key.Id), CancellationToken.None);

        Assert.Equal(expectedCode, result.Error.Code);
    }
}
