using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce;

/// <summary>Audit R34: one precedence rule for exchange rates — admin override, else live, else static fallback.</summary>
public sealed class ExchangeRatePrecedenceTests
{
    private static Dictionary<string, decimal> Map(params (string Code, decimal Rate)[] rates) =>
        rates.ToDictionary(r => r.Code, r => r.Rate, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void SeededDefaults_DoNotOverrideALiveRate()
    {
        // The platform seeds EGP 48.50 / EUR 0.92 / SAR 3.75; those must not pin the price above the live market rate.
        var live = Map(("EGP", 50.2m), ("EUR", 0.95m));
        var statics = Map(("USD", 1m), ("EGP", 48.50m), ("EUR", 0.92m), ("SAR", 3.75m));

        var rates = ExchangeRatePrecedence.Resolve(["USD", "EGP", "EUR", "SAR"], live, statics);

        Assert.Equal(50.2m, rates["EGP"]);
        Assert.Equal(0.95m, rates["EUR"]);
        Assert.Equal(3.75m, rates["SAR"]); // no live rate for it: the static value is the last-resort fallback
        Assert.Equal(1m, rates["USD"]);
    }

    [Fact]
    public void AnAdminChosenRate_WinsOverTheLiveOne()
    {
        var live = Map(("EGP", 50.2m));
        var statics = Map(("EGP", 52m));

        var rates = ExchangeRatePrecedence.Resolve(["EGP"], live, statics);

        Assert.Equal(52m, rates["EGP"]);
    }

    [Fact]
    public void ACurrencyWithNoRateAnywhere_IsLeftOut_NotZero()
    {
        var rates = ExchangeRatePrecedence.Resolve(["JPY"], Map(), Map(("EGP", 52m)));

        Assert.DoesNotContain("JPY", rates.Keys);
    }

    [Theory]
    [InlineData("EGP", 48.50, false)]
    [InlineData("EGP", 52.00, true)]
    [InlineData("EUR", 0.92, false)]
    [InlineData("JPY", 150, true)]
    [InlineData("EGP", 0, false)]
    public void OverrideDetection(string code, double value, bool expected) =>
        Assert.Equal(expected, ExchangeRatePrecedence.IsAdminOverride(code, (decimal)value));
}
