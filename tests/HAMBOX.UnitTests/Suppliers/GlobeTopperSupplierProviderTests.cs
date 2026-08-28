using System.Net;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Everything here talks to a fake <see cref="HttpMessageHandler"/> — never a real network call, never
/// real GlobeTopper credentials. Covers <see cref="GlobeTopperSupplierProvider"/>'s mapping of
/// GlobeTopper's documented, real-sandbox-confirmed request/response shapes into the generic
/// <see cref="ISupplierProvider"/> contract. Mirrors <c>BambooSupplierProviderTests</c>/
/// <c>VisoriaSupplierProviderTests</c>'s structure.
/// </summary>
public sealed class GlobeTopperSupplierProviderTests
{
    private static readonly SupplierProviderCredentials Credentials = new("HBOX", "fake-secret-token", null, null, null, null);

    private static SupplierProviderContext CreateContext() =>
        new(Guid.NewGuid(), "GLOBETOPPER-1", null, Credentials, SettingsJson: null);

    private static (GlobeTopperSupplierProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        int timeoutSeconds = 15,
        ILogger<GlobeTopperSupplierProvider>? logger = null)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(GlobeTopperProviderConstants.BaseUrl) };
        var options = Options.Create(new GlobeTopperProviderOptions { RequestTimeoutSeconds = timeoutSeconds, MaxResponseBytes = 1024 * 1024 });
        var globeTopperHttp = new GlobeTopperHttpClient(httpClient, options, NullLogger<GlobeTopperHttpClient>.Instance);
        var provider = new GlobeTopperSupplierProvider(globeTopperHttp, new MemoryCache(new MemoryCacheOptions()), logger ?? NullLogger<GlobeTopperSupplierProvider>.Instance);
        return (provider, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static SupplierPurchaseRequest CreatePurchaseRequest(int quantity = 1, decimal? unitFaceValue = 25m, string? externalProductId = "1853") =>
        new(externalProductId!, quantity, null, Guid.NewGuid().ToString(), UnitFaceValue: unitFaceValue, Currency: "USD");

    // Built via JsonSerializer (not hand-written JSON text) so a literal "operator" key — a C# reserved
    // word, unusable as an anonymous-type member name — is easy and the shape stays exactly what a real
    // GlobeTopper response looks like on the wire.
    private static Dictionary<string, object?> ProductRecord(long operatorId, string operatorName, string name, decimal min, decimal max) => new()
    {
        ["BillerID"] = 9001,
        ["name"] = name,
        ["currency"] = new { code = "USD", name = "US Dollar" },
        ["operator"] = new { id = operatorId, name = operatorName },
        ["min"] = min.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["max"] = max.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["category"] = new { name = "Gift Card" },
    };

    private static HttpResponseMessage ProductsListResponse(params (long OperatorId, string OperatorName, string Name, decimal Min, decimal Max)[] products)
    {
        var records = products.Select(p => ProductRecord(p.OperatorId, p.OperatorName, p.Name, p.Min, p.Max)).ToArray();
        return JsonResponse(HttpStatusCode.OK, new { totalRecords = records.Length, responseCode = 200, records });
    }

    private static HttpResponseMessage PurchaseSuccessResponse(long transId = 13420732) => JsonResponse(HttpStatusCode.OK, new
    {
        totalRecords = 1,
        responseCode = 200,
        records = new[]
        {
            new
            {
                trans_id = transId,
                status_description = "Success",
                extra_fields = new Dictionary<string, string> { ["Pin Number"] = "21742", ["Claim Code"] = "3322" },
            },
        },
    });

    private static HttpResponseMessage PurchaseFailureResponse(int responseCode, string message) => JsonResponse(HttpStatusCode.OK, new
    {
        totalRecords = 0,
        responseCode,
        responseMessage = message,
        records = Array.Empty<object>(),
    });

    // ===================== Authentication / request construction =====================

    [Fact]
    public async Task Requests_SendBearerHeader_CombiningApiKeyAndApiSecret()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { totalRecords = 0, responseCode = 200, records = Array.Empty<object>() })));

        await provider.TestConnectionAsync(CreateContext());

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("HBOX:fake-secret-token", handler.LastRequest!.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task MissingCredentials_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("must never reach the network without credentials"));
        var contextWithNoCredentials = new SupplierProviderContext(
            Guid.NewGuid(), "GLOBETOPPER-1", null, new SupplierProviderCredentials(null, null, null, null, null, null), SettingsJson: null);

        var result = await provider.TestConnectionAsync(contextWithNoCredentials);

        Assert.False(result.IsSuccess);
    }

    // ===================== Catalog retrieval =====================

    [Fact]
    public async Task SearchCatalogAsync_MapsOperatorIdAsExternalProductId_AndMarksAvailable()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(ProductsListResponse(
            (1853, "Aerie US", "Aerie US", 1m, 500m),
            (1111, "Google Play DE", "Google Play DE 25.00", 25m, 25m))));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Items.Count);
        var item = result.Items.Single(i => i.ExternalProductId == "1853");
        Assert.Equal("Aerie US", item.BrandName);
        Assert.Equal(1m, item.MinFaceValue);
        Assert.Equal(500m, item.MaxFaceValue);
        Assert.True(item.Available);
        // No pagination/search query parameters are sent — GlobeTopper's endpoint supports neither.
        Assert.DoesNotContain("page", handler.LastRequest!.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Real-sandbox-confirmed discrepancy, never documented anywhere: GlobeTopper formats larger
    /// denomination amounts with a thousands-separator comma inside the JSON string (real example:
    /// "Amazon UAE", <c>min: "100.00"</c>, <c>max: "1,000.00"</c>) — plain <c>decimal?</c> deserialization
    /// (even with <c>JsonNumberHandling.AllowReadingFromString</c>) throws on the comma, which broke every
    /// real catalog search until <see cref="GlobeTopperFlexibleDecimalConverter"/> was added.
    /// </summary>
    [Fact]
    public async Task SearchCatalogAsync_ThousandsSeparatorInMaxFaceValue_ParsesCorrectly()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"totalRecords":1,"responseCode":200,"records":[{"BillerID":9002,"name":"Amazon UAE",
                 "currency":{"code":"AED","name":"UAE Dirham"},"operator":{"id":2001,"name":"Amazon UAE"},
                 "min":"100.00","max":"1,000.00","category":{"name":"Gift Card"}}]}
                """, Encoding.UTF8, "application/json"),
        }));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal(100m, item.MinFaceValue);
        Assert.Equal(1000m, item.MaxFaceValue);
    }

    [Fact]
    public async Task SearchCatalogAsync_FiltersClientSideByName_NoServerSearchParamExists()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(ProductsListResponse(
            (1853, "Aerie US", "Aerie US", 1m, 500m),
            (1111, "Google Play DE", "Google Play DE 25.00", 25m, 25m))));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("google", 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Items);
        Assert.Equal("1111", result.Items.Single().ExternalProductId);
    }

    [Fact]
    public async Task SearchCatalogAsync_AuthFailure_IsReportedAsUnsuccessful_WithSafeMessage()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("fake-secret-token", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== Product mapping data =====================

    [Fact]
    public async Task GetAvailabilityAsync_PresentInCatalog_ReturnsAvailable_NeverFabricatesQuantity()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(ProductsListResponse((1853, "Aerie US", "Aerie US", 1m, 500m))));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["1853"]), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal(SupplierAvailabilityState.Available, item.State);
        Assert.Null(item.AvailableQuantity); // GlobeTopper documents no stock-quantity field — never guessed.
    }

    [Fact]
    public async Task GetAvailabilityAsync_NotInCatalog_ReturnsUnavailable_NotUnknown()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(ProductsListResponse((1853, "Aerie US", "Aerie US", 1m, 500m))));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["does-not-exist"]), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single().State);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ProviderCallFails_ReturnsUnsuccessful_NeverFabricatesUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["1853"]), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    // ===================== Purchase success / synchronous delivery =====================

    [Fact]
    public async Task PurchaseAsync_Success_ReturnsSynchronouslyDeliveredCode()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var (provider, _) = CreateProvider((req, ct) =>
        {
            capturedPath = req.RequestUri!.AbsolutePath;
            capturedBody = req.Content!.ReadAsStringAsync(ct).Result;
            return Task.FromResult(PurchaseSuccessResponse());
        });

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("13420732", result.ProviderOrderId);
        Assert.NotNull(result.DeliveredCodes);
        var code = Assert.Single(result.DeliveredCodes!);
        Assert.Contains("Pin Number: 21742", code);
        Assert.Contains("Claim Code: 3322", code);
        // Amount and product id travel in the URL path, per the documented endpoint shape.
        Assert.Equal("/api/v2/transaction/do-by-product/1853/25", capturedPath);
        Assert.Contains("order_id=", capturedBody);
        Assert.Contains("email=", capturedBody);
    }

    [Fact]
    public async Task PurchaseAsync_QuantityGreaterThanOne_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("GlobeTopper's purchase endpoint has no quantity concept — must never be called for quantity > 1."));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(quantity: 2), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_MissingFaceValue_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("must not call GlobeTopper without a face value"));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(unitFaceValue: null), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_InvalidExternalProductId_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("must not call GlobeTopper with an unparsable operator id"));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(externalProductId: "not-a-number"), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_SuccessWithNoExtraFields_ThrowsAmbiguous_NeverTrustsEmptySuccess()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"totalRecords":1,"responseCode":200,"records":[{"trans_id":1,"status_description":"Success","extra_fields":{}}]}""", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<GlobeTopperAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    // ===================== Purchase failure (definite, in-band responseCode) =====================

    [Theory]
    [InlineData(202, SupplierFulfillmentFailureCategory.ProductUnavailable)]
    [InlineData(204, SupplierFulfillmentFailureCategory.ProductUnavailable)]
    [InlineData(211, SupplierFulfillmentFailureCategory.InsufficientSupplierBalance)]
    [InlineData(212, SupplierFulfillmentFailureCategory.InsufficientSupplierBalance)]
    [InlineData(301, SupplierFulfillmentFailureCategory.AuthenticationFailed)]
    [InlineData(311, SupplierFulfillmentFailureCategory.AuthenticationFailed)]
    [InlineData(0, SupplierFulfillmentFailureCategory.UnknownProviderState)]
    [InlineData(2, SupplierFulfillmentFailureCategory.UnknownProviderState)]
    public async Task PurchaseAsync_DocumentedResponseCode_MapsToExpectedCategory_UnderHttp200(int responseCode, SupplierFulfillmentFailureCategory expected)
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(PurchaseFailureResponse(responseCode, "Some failure")));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.FailureCategory);
        Assert.Null(result.ProviderOrderId);
    }

    // ===================== Supplier error handling (real, non-200 HTTP — undocumented but defended, per the Bamboo IP-allowlist lesson) =====================

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task PurchaseAsync_RealHttpAuthFailure_IsDefiniteNotAmbiguous(HttpStatusCode status)
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(status)));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.AuthenticationFailed, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_RateLimited_IsDefiniteProviderUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.ProviderUnavailable, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_Timeout_ThrowsAmbiguous_NeverReturnsFailureResult()
    {
        var (provider, _) = CreateProvider(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return PurchaseSuccessResponse();
        }, timeoutSeconds: 1);

        await Assert.ThrowsAsync<GlobeTopperAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task PurchaseAsync_ServerError_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<GlobeTopperAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task PurchaseAsync_MalformedJsonBody_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-valid-json", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<GlobeTopperAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    // ===================== Retry / idempotency behavior =====================

    /// <summary>
    /// GlobeTopper documents no idempotency mechanism at all — the same reference sent twice makes two
    /// independent, real purchase calls (each one succeeding here, proving the provider itself never
    /// invents a dedup guarantee that doesn't exist). The actual "never double-purchase" safety for this
    /// provider comes entirely from <c>SupplierFulfillmentService</c>'s claim/state-machine (shared
    /// infrastructure, unchanged) never re-submitting an already-Submitted/Submitted fulfillment — not
    /// from anything GlobeTopper-specific. See docs/integrations/suppliers/README.md.
    /// </summary>
    [Fact]
    public async Task PurchaseAsync_RetriedReferenceId_MakesTwoIndependentCalls_NoProviderSideDedup()
    {
        var callCount = 0;
        var (provider, _) = CreateProvider((req, ct) =>
        {
            callCount++;
            return Task.FromResult(PurchaseSuccessResponse(transId: 1000 + callCount));
        });

        var request = CreatePurchaseRequest();
        var first = await provider.PurchaseAsync(request, CreateContext());
        var second = await provider.PurchaseAsync(request, CreateContext());

        Assert.Equal(2, callCount);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.ProviderOrderId, second.ProviderOrderId);
    }

    // ===================== Order status mapping =====================

    [Fact]
    public async Task GetOrderStatusAsync_Success_ReturnsSucceededWithCodes()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"totalRecords":1,"responseCode":200,"records":[{"trans_id":555,"status_description":"Success","extra_fields":{"Pin Number":"999"}}]}""", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "555"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Succeeded, result.Status);
        Assert.Equal("555", result.ProviderOrderId);
        Assert.Single(result.DeliveredCodes!);
        Assert.Contains("/transaction/search-transactions/555", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetOrderStatusAsync_NoProviderOrderId_ThrowsAmbiguous_DocumentedGap()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("must never call GlobeTopper without a trans_id — there is no lookup-by-reference endpoint"));

        await Assert.ThrowsAsync<GlobeTopperAmbiguousResponseException>(
            () => provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), null), CreateContext()));
    }

    [Fact]
    public async Task GetOrderStatusAsync_UnrecognizedStatusDescription_ReturnsUnknown_NeverGuessed()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"totalRecords":1,"responseCode":200,"records":[{"trans_id":555,"status_description":"Processing","extra_fields":{}}]}""", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "555"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Unknown, result.Status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_TransactionNotFound_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"totalRecords":0,"responseCode":200,"records":[]}""", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<GlobeTopperAmbiguousResponseException>(
            () => provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "555"), CreateContext()));
    }

    // ===================== Credential validation =====================

    [Fact]
    public async Task ValidateCredentialsAsync_Success_ReturnsValid()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"totalRecords":1,"responseCode":200,"records":[{"agent_id":21742,"currency":{"code":"USD","name":"US Dollar"}}]}""", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.ValidateCredentialsAsync(CreateContext());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_Unauthorized_ReturnsInvalid_WithSafeMessage()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await provider.ValidateCredentialsAsync(CreateContext());

        Assert.False(result.IsValid);
        Assert.DoesNotContain("fake-secret-token", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== No credential/code leakage into logs or responses =====================

    [Fact]
    public async Task Provider_NeverLogsCredentialsOrDeliveredCodes()
    {
        var recordingLogger = new RecordingLogger<GlobeTopperSupplierProvider>();
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(PurchaseSuccessResponse()), logger: recordingLogger);

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        foreach (var message in recordingLogger.Messages)
        {
            Assert.DoesNotContain("fake-secret-token", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("21742", message, StringComparison.Ordinal); // the delivered Pin Number
            Assert.DoesNotContain("Bearer ", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Provider_NeverLeaksCredentialsInFailureMessage()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("fake-secret-token", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HBOX", result.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
