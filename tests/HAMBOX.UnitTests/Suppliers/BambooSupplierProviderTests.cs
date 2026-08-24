using System.Net;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Everything here talks to a fake <see cref="HttpMessageHandler"/> — never a real network call, never
/// real Bamboo sandbox credentials. Covers <see cref="BambooSupplierProvider"/>'s mapping of Bamboo's
/// documented request/response shapes into the generic <see cref="ISupplierProvider"/> contract.
/// </summary>
public sealed class BambooSupplierProviderTests
{
    private static readonly SupplierProviderCredentials Credentials = new("client-id", "client-secret", null, null, null, null);
    private static readonly string SettingsJson = JsonSerializer.Serialize(new { accountId = 555 });

    private static SupplierProviderContext CreateContext() =>
        new(Guid.NewGuid(), "BAMBOO-1", null, Credentials, SettingsJson);

    private static (BambooSupplierProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        int timeoutSeconds = 15,
        ILogger<BambooSupplierProvider>? logger = null)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BambooProviderConstants.BaseUrl) };
        var options = Options.Create(new BambooProviderOptions { RequestTimeoutSeconds = timeoutSeconds, MaxResponseBytes = 1024 * 1024 });
        var bambooHttp = new BambooHttpClient(httpClient, options, NullLogger<BambooHttpClient>.Instance);
        var provider = new BambooSupplierProvider(bambooHttp, logger ?? NullLogger<BambooSupplierProvider>.Instance);
        return (provider, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static SupplierPurchaseRequest CreatePurchaseRequest(int quantity = 1) =>
        new("483243", quantity, null, Guid.NewGuid().ToString(), UnitFaceValue: 25m, Currency: "USD");

    // A. Successful Bamboo purchase (full documented async lifecycle: accepted, then confirmed via GetOrder)
    [Fact]
    public async Task PurchaseAsync_Accepted_ReturnsSubmittedWithNoCodesYet()
    {
        // Confirmed against the real sandbox (Phase 5): a successful Place Order body is a bare JSON
        // string (the RequestId), not an object — the documentation prose was misleading here.
        var (provider, handler) = CreateProvider((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "71ac2817-e51a-438a-bb7b-5ffdda23603c")));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        // The RequestId echo is not a distinct Bamboo order reference (it's our own GUID reflected
        // back) — must not be recorded as ProviderOrderId, or the domain's immutability guard rejects
        // the real, different orderId GetOrderStatusAsync reports later during reconciliation.
        Assert.Null(result.ProviderOrderId);
        Assert.Null(result.DeliveredCodes); // Place Order never returns codes synchronously — must reconcile
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("/orders/checkout", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Succeeded_ReturnsOnlySoldCards()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            orderId = 6779294,
            requestId = "71ac2817-e51a-438a-bb7b-5ffdda23603c",
            status = "Succeeded",
            items = new[]
            {
                new
                {
                    cards = new object[]
                    {
                        new { cardCode = "CODE-1", pin = "111", status = "Sold" },
                        new { cardCode = "CODE-2", pin = (string?)null, status = "Sold" },
                        new { cardCode = "CODE-3", pin = "333", status = "Failed" }, // must be excluded
                    },
                },
            },
        })));

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), "6779294");
        var result = await provider.GetOrderStatusAsync(query, CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Succeeded, result.Status);
        Assert.NotNull(result.DeliveredCodes);
        Assert.Equal(2, result.DeliveredCodes!.Count); // only the two "Sold" cards
        Assert.Contains("CODE-1:111", result.DeliveredCodes);
        Assert.Contains("CODE-2", result.DeliveredCodes); // no pin -> code alone
        Assert.DoesNotContain(result.DeliveredCodes, c => c.StartsWith("CODE-3"));
    }

    // K. Partial fulfillment mapping
    [Fact]
    public async Task GetOrderStatusAsync_PartialFailed_ReturnsOnlyDeliveredCards()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            orderId = 100,
            requestId = Guid.NewGuid().ToString(),
            status = "PartialFailed",
            items = new[]
            {
                new
                {
                    cards = new object[]
                    {
                        new { cardCode = "OK-1", pin = (string?)null, status = "Sold" },
                        new { cardCode = "BAD-1", pin = (string?)null, status = "Failed" },
                    },
                },
            },
        })));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "100"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.PartialFailed, result.Status);
        Assert.Single(result.DeliveredCodes!);
        Assert.Equal("OK-1", result.DeliveredCodes!.Single());
    }

    // B. Bamboo definitive failure (documented reason code)
    [Theory]
    [InlineData("InsufficientBalance", SupplierFulfillmentFailureCategory.InsufficientSupplierBalance)]
    [InlineData("ProductIsOutOfStock", SupplierFulfillmentFailureCategory.ProductUnavailable)]
    [InlineData("InvalidProductIds", SupplierFulfillmentFailureCategory.InvalidProduct)]
    [InlineData("InvalidDenomination", SupplierFulfillmentFailureCategory.InvalidDenomination)]
    [InlineData("WrongAccount", SupplierFulfillmentFailureCategory.InvalidConfiguration)]
    [InlineData("UserNotEnabled", SupplierFulfillmentFailureCategory.AuthenticationFailed)]
    [InlineData("SomeFutureUndocumentedCode", SupplierFulfillmentFailureCategory.UnknownProviderState)]
    public async Task PurchaseAsync_DefiniteReasonCode_MapsToExpectedCategory(string reasonCode, SupplierFulfillmentFailureCategory expected)
    {
        var (provider, _) = CreateProvider((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new { reasonCode })));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.FailureCategory);
    }

    // F. Bamboo authentication failure
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task PurchaseAsync_AuthFailure_IsDefiniteNotAmbiguous(HttpStatusCode status)
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.AuthenticationFailed, result.FailureCategory);
    }

    // C. Bamboo timeout -> must throw (ambiguous), never return a definite failure
    [Fact]
    public async Task PurchaseAsync_Timeout_ThrowsAmbiguous_NeverReturnsFailureResult()
    {
        var (provider, _) = CreateProvider(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct); // outlasts the 1s client timeout below
            return JsonResponse(HttpStatusCode.OK, "should-never-be-seen");
        }, timeoutSeconds: 1);

        await Assert.ThrowsAsync<BambooAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    // D. Bamboo 5xx -> ambiguous
    [Fact]
    public async Task PurchaseAsync_ServerError_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain"),
        }));

        await Assert.ThrowsAsync<BambooAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    // E. Bamboo malformed response -> ambiguous, never assumed success
    [Fact]
    public async Task PurchaseAsync_MalformedJsonBody_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-valid-json", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<BambooAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task PurchaseAsync_SuccessStatusWithNoRequestId_ThrowsAmbiguous_NeverAssumesSuccessFromHttp200Alone()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { })));

        await Assert.ThrowsAsync<BambooAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    /// <summary>
    /// The single most important idempotency-safety mapping: Bamboo's own <c>OrderAlreadyExists</c>
    /// response must NEVER become a definite failure (which would let a caller mistakenly conclude the
    /// shortfall needs a fresh purchase attempt) — it must be ambiguous, forcing reconciliation via
    /// GetOrder to discover what the earlier, already-accepted request actually resulted in.
    /// </summary>
    [Fact]
    public async Task PurchaseAsync_OrderAlreadyExists_ThrowsAmbiguous_NotDefiniteFailure()
    {
        var (provider, _) = CreateProvider((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new { reasonCode = "OrderAlreadyExists" })));

        var ex = await Record.ExceptionAsync(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));

        Assert.IsType<BambooAmbiguousResponseException>(ex);
    }

    /// <summary>
    /// Regression test for a real bug found against the actual sandbox (Phase 5): Bamboo's real
    /// duplicate-RequestId body is <c>{"message":"OrderAlreadyExists"}</c> — no "reasonCode" field at
    /// all. Before the fix, this shape silently fell through to a DEFINITE UnknownProviderState
    /// failure instead of the required ambiguous/reconciliation path, exactly the double-purchase risk
    /// this whole design exists to prevent.
    /// </summary>
    [Fact]
    public async Task PurchaseAsync_OrderAlreadyExists_RealSandboxShape_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new { message = "OrderAlreadyExists" })));

        var ex = await Record.ExceptionAsync(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));

        Assert.IsType<BambooAmbiguousResponseException>(ex);
    }

    // Rate limiting is a definite, known outcome (rejected before processing) — not ambiguous.
    [Fact]
    public async Task PurchaseAsync_RateLimited_IsDefiniteProviderUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.ProviderUnavailable, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_MissingAccountIdInSettings_FailsClosed_WithoutAnyHttpCall()
    {
        var handler = new FakeHttpMessageHandler((req, ct) => throw new InvalidOperationException("Must not call Bamboo when configuration is invalid."));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BambooProviderConstants.BaseUrl) };
        var bambooHttp = new BambooHttpClient(httpClient, Options.Create(new BambooProviderOptions()), NullLogger<BambooHttpClient>.Instance);
        var provider = new BambooSupplierProvider(bambooHttp, NullLogger<BambooSupplierProvider>.Instance);

        var contextWithNoSettings = new SupplierProviderContext(Guid.NewGuid(), "BAMBOO-1", null, Credentials, SettingsJson: null);

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), contextWithNoSettings);

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    // Catalog search flattens brand-level entries into one selectable item per denomination, using
    // exactly Bamboo's documented Get Catalog schema (docs.bamboocardportal.com/docs/get-catalog).
    [Fact]
    public async Task SearchCatalogAsync_FlattensBrandsIntoOneItemPerDenomination()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            pageIndex = 0,
            pageSize = 20,
            count = 1,
            items = new object[]
            {
                new
                {
                    internalId = "brand-1",
                    name = "Amazon Gift Card",
                    countryCode = "US",
                    currencyCode = "USD",
                    logoUrl = "https://example.com/amazon.png",
                    products = new object[]
                    {
                        new { id = 439685, name = "$10", minFaceValue = 10m, maxFaceValue = 10m, count = 5, price = new { min = 10m, max = 10m, currencyCode = "USD" } },
                        new { id = 439686, name = "$25", minFaceValue = 25m, maxFaceValue = 25m, count = (int?)null, price = new { min = 25m, max = 25m, currencyCode = "USD" } },
                    },
                },
            },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("amazon", 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Items.Count);

        var ten = result.Items.Single(i => i.ExternalProductId == "439685");
        Assert.Contains("Amazon Gift Card", ten.Name);
        Assert.Equal("Amazon Gift Card", ten.BrandName);
        Assert.Equal("USD", ten.Currency);
        Assert.Equal(10m, ten.MinFaceValue);
        Assert.Equal(10m, ten.MaxFaceValue);
        Assert.True(ten.Available); // count = 5 -> available

        var twentyFive = result.Items.Single(i => i.ExternalProductId == "439686");
        Assert.True(twentyFive.Available); // count = null -> treated as available (no stock signal reported as unavailable)

        // Name filter passed through as Bamboo's documented "Name" query param; page converted to zero-based PageIndex.
        Assert.Contains("Name=amazon", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("PageIndex=0", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task SearchCatalogAsync_NeverExposesCredentialsOrRawResponseBeyondSafeFields()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            pageIndex = 0,
            pageSize = 20,
            count = 1,
            items = new object[]
            {
                new
                {
                    internalId = "brand-1",
                    name = "Amazon Gift Card",
                    currencyCode = "USD",
                    products = new object[] { new { id = 1, name = "$10", minFaceValue = 10m, maxFaceValue = 10m, count = 1 } },
                },
            },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        // SupplierCatalogItem's own shape has no field that could carry a secret — asserted structurally
        // so this fails loudly if a future edit adds one.
        var itemProperties = typeof(SupplierCatalogItem).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(itemProperties, p => p.Contains("Secret", StringComparison.OrdinalIgnoreCase) || p.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) || p.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("client-secret", result.Items.Single().Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchCatalogAsync_EmptyItems_ReturnsEmptySuccessNotFailure()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { pageIndex = 0, pageSize = 20, count = 0, items = Array.Empty<object>() })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("nonexistent-brand", 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchCatalogAsync_AuthFailure_IsReportedAsUnsuccessful_WithSafeMessage()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("client-secret", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // Test Connection must expose only safe, non-secret facts (account id, sandbox/production, currency,
    // active status) — never Balance, never anything from SupplierProviderCredentials.
    [Fact]
    public async Task TestConnectionAsync_Success_SummarizesSafeAccountFacts_NeverBalanceOrCredentials()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            accounts = new[]
            {
                new { id = 12345, currency = "USD", balance = 999999.99m, isActive = true, sandboxMode = true },
            },
        })));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Contains("12345", result.Message);
        Assert.Contains("Sandbox", result.Message);
        Assert.Contains("USD", result.Message);
        Assert.Contains("Active", result.Message);
        Assert.DoesNotContain("999999.99", result.Message);
        Assert.DoesNotContain("client-secret", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client-id", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectionAsync_NoAccounts_ReportsZero_NeverThrows()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { accounts = Array.Empty<object>() })));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Contains("0 account", result.Message);
    }

    // ===================== GetAvailabilityAsync (Supplier Availability phase) =====================

    [Fact]
    public async Task GetAvailabilityAsync_MappedProductWithStock_ReturnsAvailable()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            pageIndex = 0,
            pageSize = 200,
            count = 1,
            items = new object[]
            {
                new
                {
                    internalId = "brand-1",
                    name = "Amazon Gift Card",
                    currencyCode = "USD",
                    products = new object[] { new { id = 439685, name = "$10", minFaceValue = 10m, maxFaceValue = 10m, count = 5 } },
                },
            },
        })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["439685"]), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal(SupplierAvailabilityState.Available, item.State);
        Assert.Equal(5, item.AvailableQuantity);
        // Never a per-mapping call — one catalog request resolves the whole requested batch.
        Assert.Contains("PageIndex=0", handler.LastRequest!.RequestUri!.Query);
        Assert.DoesNotContain("Name=", handler.LastRequest.RequestUri.Query); // unfiltered pull, not a search
    }

    [Fact]
    public async Task GetAvailabilityAsync_MappedProductWithZeroCount_ReturnsUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            pageIndex = 0,
            pageSize = 200,
            count = 1,
            items = new object[]
            {
                new { internalId = "brand-1", name = "Amazon Gift Card", currencyCode = "USD",
                    products = new object[] { new { id = 439685, name = "$10", minFaceValue = 10m, maxFaceValue = 10m, count = 0 } } },
            },
        })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["439685"]), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single().State);
    }

    [Fact]
    public async Task GetAvailabilityAsync_RequestedIdNotInCatalog_ReturnsUnavailable_NotUnknown()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            pageIndex = 0,
            pageSize = 200,
            count = 0,
            items = Array.Empty<object>(),
        })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["does-not-exist"]), CreateContext());

        Assert.True(result.IsSuccess); // the provider call itself succeeded — it just has a definite negative answer
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single().State);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ProviderCallFails_ReturnsUnsuccessful_NeverFabricatesUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["439685"]), CreateContext());

        // A failed provider call must report IsSuccess=false with NO items — never guess Unavailable for
        // requested ids just because the call itself failed. The caller (SupplierAvailabilityService) is
        // responsible for turning this into Unknown, never Unavailable.
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAvailabilityAsync_MissingCredentials_ReturnsUnsuccessful()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("must never reach the network without credentials"));
        var contextWithNoCredentials = new SupplierProviderContext(
            Guid.NewGuid(), "BAMBOO-1", null, new SupplierProviderCredentials(null, null, null, null, null, null), SettingsJson);

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["439685"]), contextWithNoCredentials);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAvailabilityAsync_MultipleRequestedIds_ResolvedFromOneCatalogPull()
    {
        var callCount = 0;
        var (provider, _) = CreateProvider((req, ct) =>
        {
            callCount++;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, new
            {
                pageIndex = 0,
                pageSize = 200,
                count = 1,
                items = new object[]
                {
                    new
                    {
                        internalId = "brand-1", name = "Amazon Gift Card", currencyCode = "USD",
                        products = new object[]
                        {
                            new { id = 1, name = "$10", minFaceValue = 10m, maxFaceValue = 10m, count = 3 },
                            new { id = 2, name = "$25", minFaceValue = 25m, maxFaceValue = 25m, count = 0 },
                        },
                    },
                },
            }));
        });

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["1", "2", "3"]), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, callCount); // one HTTP call resolved all three requested ids, not three separate calls
        Assert.Equal(SupplierAvailabilityState.Available, result.Items.Single(i => i.ExternalProductId == "1").State);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single(i => i.ExternalProductId == "2").State);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single(i => i.ExternalProductId == "3").State); // not in catalog
    }

    // T. Credentials/redemption secrets never appear in logs, across every path exercised above.
    [Fact]
    public async Task Provider_NeverLogsCredentialsOrCardSecrets()
    {
        var recordingLogger = new RecordingLogger<BambooSupplierProvider>();
        var (provider, _) = CreateProvider(
            (req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, Guid.NewGuid().ToString())),
            logger: recordingLogger);

        await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        var (failProvider, _) = CreateProvider(
            (req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new { reasonCode = "InsufficientBalance" })),
            logger: recordingLogger);
        await failProvider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        foreach (var message in recordingLogger.Messages)
        {
            Assert.DoesNotContain("client-secret", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("client-id", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Basic ", message, StringComparison.Ordinal); // no Authorization header value
        }
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
