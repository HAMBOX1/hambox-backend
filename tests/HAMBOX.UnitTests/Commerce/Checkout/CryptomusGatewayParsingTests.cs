using System.Net;
using System.Text;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Regression for a production failure the new reconcile sweep exposed: Cryptomus sends amounts as JSON strings
/// ("payment_amount_usd": "25.50"), which the gateway client could not read — so no paid invoice was verifiable.
/// </summary>
public sealed class CryptomusGatewayParsingTests
{
    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private static CryptomusPaymentGateway Create(string json) =>
        new(
            new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("https://api.cryptomus.test") },
            new FakePaymentGatewayConfigurationProvider(),
            NullLogger<CryptomusPaymentGateway>.Instance);

    [Fact]
    public async Task PaymentInfo_WithAmountsAsStrings_IsParsed()
    {
        const string json = """
            {"state":0,"result":{"uuid":"u-1","order_id":"HB-1","url":"https://pay.example/u-1","payment_status":"paid",
             "is_final":true,"payment_amount_usd":"25.50","payer_currency":"USDT"}}
            """;

        var result = await Create(json).GetPaymentInfoByOrderIdAsync("HB-1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsSuccessfulPayment);
        Assert.Equal(25.50m, result.Value.PaymentAmountUsd);
        Assert.Equal("USDT", result.Value.PayerCurrency);
    }

    [Fact]
    public async Task PaymentInfo_WithAmountsAsNumbers_IsStillParsed()
    {
        const string json = """
            {"state":0,"result":{"uuid":"u-1","order_id":"HB-1","payment_status":"paid","is_final":true,"payment_amount_usd":12.75}}
            """;

        var result = await Create(json).GetPaymentInfoByOrderIdAsync("HB-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(12.75m, result.Value.PaymentAmountUsd);
    }

    [Fact]
    public async Task PaymentInfo_WhileUnpaid_HasNoAmountAndIsStillProcessing()
    {
        const string json = """
            {"state":0,"result":{"uuid":"u-1","order_id":"HB-1","payment_status":"check","is_final":false,"payment_amount_usd":null}}
            """;

        var result = await Create(json).GetPaymentInfoByOrderIdAsync("HB-1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsStillProcessing);
        Assert.Null(result.Value.PaymentAmountUsd);
    }
}
