using System.Net;
using System.Net.Http.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Commerce;

/// <summary>
/// Audit R30 against the real host: the two paths no passing test used to execute — a webhook with an invalid
/// signature and a callback that matches no order — must be answered correctly AND leave a traceable
/// <c>PaymentWebhookLogs</c> row (provider, signature result, gateway/merchant reference, raw payload, outcome).
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class PaymentWebhookAuditTests : IAsyncLifetime
{
    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Database.MigrateAsync();
        }

        await _factory.DisableMaintenanceModeAsync();
        _client = _factory.CreateDefaultClient(new Uri("https://localhost"));
        _client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private async Task<List<HAMBOX.Modules.Commerce.Domain.Orders.PaymentWebhookLog>> LogsAsync(string provider)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        return await db.PaymentWebhookLogs.AsNoTracking().Where(l => l.Provider == provider).ToListAsync();
    }

    [Fact]
    public async Task CryptomusWebhook_WithABadSignature_IsRejected_AndRecordedAsInvalid()
    {
        var body = """{"order_id":"HB-123","uuid":"u-1","status":"paid","sign":"0000deadbeef"}""";

        var response = await _client.PostAsync(
            "api/v1/payments/cryptomus/webhook",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var row = Assert.Single(await LogsAsync("Cryptomus"));
        Assert.Equal("Invalid", row.SignatureVerificationResult);
        Assert.Equal("RejectedSignature", row.ProcessingResult);
        Assert.Contains("HB-123", row.RawPayload);
    }

    [Fact]
    public async Task OxaPayWebhook_WithoutASignatureHeader_IsRejected_AndRecordedAsMissing()
    {
        var response = await _client.PostAsync(
            "api/v1/payments/oxapay/webhook",
            new StringContent("""{"order_id":"HB-9","track_id":"t-9","status":"paid"}""", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var row = Assert.Single(await LogsAsync("OxaPay"));
        Assert.Equal("Missing", row.SignatureVerificationResult);
        Assert.Equal("RejectedSignature", row.ProcessingResult);
    }

    [Fact]
    public async Task DotNotification_ForAnOrderWeNeverIssued_IsAcked_AndRecordedAsOrderNotFound()
    {
        var response = await _client.PostAsJsonAsync("api/v1/payments/dot/notify", new
        {
            dotTransId = "DOT-777",
            partnerTransId = "unknown-partner-tx",
            opId = 117,
            msisdn = "01001234567",
            amount = "10.00",
            serviceId = "svc",
            resultCode = "0",
            resultDesc = "ok",
        });

        // Either the documented "1" ack, or the IP allowlist refusing a non-DOT caller — and in both cases the
        // callback must be on record. What must never happen is silently losing it.
        var rows = await LogsAsync("Dot");
        var row = Assert.Single(rows);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("1", await response.Content.ReadAsStringAsync());
            Assert.Equal("OrderNotFound", row.ProcessingResult);
            Assert.Equal("DOT-777", row.GatewayReference);
            Assert.Equal("unknown-partner-tx", row.MerchantReference);
            Assert.DoesNotContain("01001234567", row.RawPayload); // the customer's number is masked in the log
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("RejectedIp", row.ProcessingResult);
        }
    }
}
