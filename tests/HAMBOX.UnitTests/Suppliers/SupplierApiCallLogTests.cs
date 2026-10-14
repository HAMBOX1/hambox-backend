using System.Net;
using System.Text;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Audit R40: every outbound supplier call is logged for dispute reconstruction — redacted, truncated, tied to its
/// fulfillment attempt — without ever storing a delivered code or a credential, and without disturbing the call.
/// </summary>
public sealed class SupplierApiCallLogTests
{
    // ─── sanitizer ──────────────────────────────────────────────────────

    [Fact]
    public void CredentialsAndCodes_AreRedactedByFieldName_AtAnyDepth()
    {
        const string body = """
            {"productId":4812,"quantity":2,"apiKey":"SECRET-1","auth":{"password":"hunter2","user":"shop"},
             "cards":[{"code":"AAAA-BBBB","pin":"1234","serialNumber":"SN-9","faceValue":10}],"requestId":"r-1"}
            """;

        var clean = SupplierApiLogSanitizer.SanitizeBody(body)!;

        Assert.DoesNotContain("SECRET-1", clean);
        Assert.DoesNotContain("hunter2", clean);
        Assert.DoesNotContain("AAAA-BBBB", clean);
        Assert.DoesNotContain("1234", clean);
        Assert.DoesNotContain("SN-9", clean);

        // What a dispute needs survives.
        Assert.Contains("4812", clean);
        Assert.Contains("r-1", clean);
        Assert.Contains("\"faceValue\":10", clean);
    }

    [Fact]
    public void QueryStringSecrets_AreMasked_ButThePathAndOtherParametersStay()
    {
        var uri = new Uri("https://api.supplier.test/v1/orders/123?apikey=TOPSECRET&page=2&token=abc");

        var clean = SupplierApiLogSanitizer.SanitizeUrl(uri, "(unknown)");

        Assert.DoesNotContain("TOPSECRET", clean);
        Assert.DoesNotContain("token=abc", clean);
        Assert.Contains("/v1/orders/123", clean);
        Assert.Contains("page=2", clean);
    }

    [Fact]
    public void ASuccessSummary_KeepsIdsAndStatus_AndNeverACode()
    {
        const string body = """
            {"requestId":"abc-123","status":"Completed","orderNumber":"ORD-9",
             "items":[{"productId":7,"cards":[{"code":"GAME-KEY-0001","pin":"9999"}]}]}
            """;

        var summary = SupplierApiLogSanitizer.Summarise(body)!;

        Assert.Contains("abc-123", summary);
        Assert.Contains("Completed", summary);
        Assert.DoesNotContain("GAME-KEY-0001", summary);
        Assert.DoesNotContain("9999", summary);
    }

    [Fact]
    public void NonJsonBodies_AreShortened_NotKeptWhole()
    {
        var html = "<html>" + new string('x', 2000) + "</html>";

        var clean = SupplierApiLogSanitizer.SanitizeBody(html)!;

        Assert.True(clean.Length < 400);
    }

    // ─── handler ────────────────────────────────────────────────────────

    private sealed class CannedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection reset");
    }

    private static (HttpClient Client, ServiceProvider Provider) CreateClient(HttpMessageHandler inner)
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddSingleton<HAMBOX.Application.Abstractions.ICodeProtector>(new FakeCodeProtector());
        services.AddDbContext<SuppliersDbContext>(o => o.UseInMemoryDatabase($"suppliers-{databaseName}"));
        var provider = services.BuildServiceProvider();

        var logging = new SupplierApiCallLoggingHandler(
            "Bamboo", provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SupplierApiCallLoggingHandler>.Instance)
        {
            InnerHandler = inner,
        };

        return (new HttpClient(logging) { BaseAddress = new Uri("https://api.supplier.test") }, provider);
    }

    private static async Task<List<HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierApiCallLog>> LogsAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SuppliersDbContext>().SupplierApiCallLogs.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task ASuccessfulPurchase_IsLoggedWithItsReference_ButWithoutTheDeliveredCode()
    {
        var (client, provider) = CreateClient(new CannedHandler(
            HttpStatusCode.OK,
            """{"requestId":"req-77","status":"Completed","cards":[{"code":"REAL-CODE-1234","pin":"4321"}]}"""));
        var fulfillmentId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        HttpResponseMessage response;
        using (SupplierCallScope.Begin(new SupplierCallInfo(fulfillmentId, referenceId, Guid.NewGuid(), orderId, 2)))
        {
            response = await client.PostAsync(
                "/api/v1/orders?apikey=SHHH",
                new StringContent("""{"productId":42,"quantity":1,"apiKey":"SHHH"}""", Encoding.UTF8, "application/json"));
        }

        // The caller still reads the full body — logging must be invisible to it.
        Assert.Contains("REAL-CODE-1234", await response.Content.ReadAsStringAsync());

        var log = Assert.Single(await LogsAsync(provider));
        Assert.Equal("Bamboo", log.ProviderKey);
        Assert.Equal("POST", log.Method);
        Assert.Equal(200, log.StatusCode);
        Assert.True(log.Succeeded);
        Assert.Equal(fulfillmentId, log.FulfillmentId);
        Assert.Equal(referenceId, log.HamboxReferenceId);
        Assert.Equal(orderId, log.OrderId);
        Assert.Equal(2, log.Attempt);

        var stored = string.Join(' ', log.Url, log.RequestBody, log.ResponseBody, log.ResponseSummary);
        Assert.DoesNotContain("REAL-CODE-1234", stored);
        Assert.DoesNotContain("4321", stored);
        Assert.DoesNotContain("SHHH", stored);
        Assert.Contains("req-77", log.ResponseSummary);
        Assert.Contains("42", log.RequestBody);
    }

    [Fact]
    public async Task AFailedCall_KeepsTheRedactedErrorBody_ForTheDispute()
    {
        var (client, provider) = CreateClient(new CannedHandler(
            HttpStatusCode.UnprocessableEntity, """{"error":"InsufficientBalance","message":"Balance too low","token":"leak"}"""));

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var log = Assert.Single(await LogsAsync(provider));
        Assert.False(log.Succeeded);
        Assert.Equal(422, log.StatusCode);
        Assert.Contains("InsufficientBalance", log.ResponseBody);
        Assert.DoesNotContain("leak", log.ResponseBody);
    }

    [Fact]
    public async Task ATransportFailure_IsLogged_AndStillThrownToTheCaller()
    {
        var (client, provider) = CreateClient(new ThrowingHandler());

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/api/v1/accounts"));

        var log = Assert.Single(await LogsAsync(provider));
        Assert.False(log.Succeeded);
        Assert.Null(log.StatusCode);
        Assert.Contains("connection reset", log.ErrorMessage);
    }

    [Fact]
    public async Task ABrokenLogStore_NeverBreaksTheSupplierCall()
    {
        // No SuppliersDbContext registered at all: the log write fails, the call must not.
        var provider = new ServiceCollection().BuildServiceProvider();
        var logging = new SupplierApiCallLoggingHandler(
            "Bamboo", provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SupplierApiCallLoggingHandler>.Instance)
        {
            InnerHandler = new CannedHandler(HttpStatusCode.OK, """{"ok":true}"""),
        };
        using var client = new HttpClient(logging) { BaseAddress = new Uri("https://api.supplier.test") };

        var response = await client.GetAsync("/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
