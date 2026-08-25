using HAMBOX.Modules.Commerce.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Regression coverage for a critical security fix: <see cref="ImmediatePaymentProvider"/> must
/// never match a payment method outside Development. Before this fix, `CanHandle` matched ANY
/// method that wasn't exactly "development" in every environment, including Production — meaning
/// a checkout request with an unrecognized `paymentMethod` (e.g. "card", a typo, anything) would be
/// marked paid for $0 by <see cref="CheckoutCommandHandler"/>'s provider lookup with no real charge.
/// </summary>
public sealed class ImmediatePaymentProviderTests
{
    [Theory]
    [InlineData("card")]
    [InlineData("stripe")]
    [InlineData("")]
    [InlineData("anything-unrecognized")]
    public void CanHandle_ReturnsFalse_InProduction_RegardlessOfPaymentMethod(string paymentMethod)
    {
        var provider = new ImmediatePaymentProvider(new FakeHostEnvironment("Production"), NullLogger<ImmediatePaymentProvider>.Instance);

        Assert.False(provider.CanHandle(paymentMethod));
    }

    [Fact]
    public void CanHandle_ReturnsFalse_InProduction_ForDevelopmentKeyToo()
    {
        var provider = new ImmediatePaymentProvider(new FakeHostEnvironment("Production"), NullLogger<ImmediatePaymentProvider>.Instance);

        Assert.False(provider.CanHandle(DevelopmentPaymentProvider.PaymentMethodKey));
    }

    [Theory]
    [InlineData("card")]
    [InlineData("anything-unrecognized")]
    public void CanHandle_ReturnsTrue_InDevelopment_ForNonDevelopmentMethod(string paymentMethod)
    {
        var provider = new ImmediatePaymentProvider(new FakeHostEnvironment("Development"), NullLogger<ImmediatePaymentProvider>.Instance);

        Assert.True(provider.CanHandle(paymentMethod));
    }

    [Fact]
    public void CanHandle_ReturnsFalse_InDevelopment_ForDevelopmentKey_LeavingItToDevelopmentPaymentProvider()
    {
        var provider = new ImmediatePaymentProvider(new FakeHostEnvironment("Development"), NullLogger<ImmediatePaymentProvider>.Instance);

        Assert.False(provider.CanHandle(DevelopmentPaymentProvider.PaymentMethodKey));
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HAMBOX.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
