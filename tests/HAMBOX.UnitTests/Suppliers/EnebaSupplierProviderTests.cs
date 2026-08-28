using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Everything here talks to a fake <see cref="HttpMessageHandler"/> — never a real network call, never
/// real Eneba credentials. Covers <see cref="EnebaSupplierProvider"/>'s mapping of Eneba's documented
/// GraphQL request/response shapes into the generic <see cref="ISupplierProvider"/> contract, including
/// the OAuth2 token flow and the key-export archive extraction. Mirrors
/// <c>BambooSupplierProviderTests</c>/<c>GlobeTopperSupplierProviderTests</c>'s structure.
/// </summary>
public sealed class EnebaSupplierProviderTests
{
    private static readonly string OAuthSettingsJson = JsonSerializer.Serialize(new
    {
        authId = "AUTH-ID-123",
        authSecret = "fake-auth-secret",
        accountEmail = "buyer@example.com",
    });

    private static readonly SupplierProviderCredentials Credentials = new(null, null, null, null, null, OAuthSettingsJson);

    private static SupplierProviderContext CreateContext() =>
        new(Guid.NewGuid(), "ENEBA-1", null, Credentials, SettingsJson: null);

    private static (EnebaSupplierProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>>? graphQlResponder = null,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? tokenResponder = null,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? archiveResponder = null,
        int timeoutSeconds = 15,
        int exportPollAttempts = 2,
        int exportPollDelaySeconds = 0)
    {
        var handler = new FakeHttpMessageHandler(async (req, ct) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/oauth/token")
            {
                return tokenResponder is not null ? await tokenResponder(req, ct) : DefaultTokenResponse();
            }

            if (path == EnebaProviderConstants.GraphQlPath)
            {
                var body = req.Content is null ? string.Empty : await req.Content.ReadAsStringAsync(ct);
                return graphQlResponder is not null
                    ? await graphQlResponder(req, body, ct)
                    : throw new InvalidOperationException("No graphQlResponder configured for this test.");
            }

            return archiveResponder is not null
                ? await archiveResponder(req, ct)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(EnebaProviderConstants.BaseUrl) };
        var options = Options.Create(new EnebaProviderOptions
        {
            RequestTimeoutSeconds = timeoutSeconds,
            MaxResponseBytes = 1024 * 1024,
            MaxArchiveBytes = 4 * 1024 * 1024,
            ExportPollAttempts = exportPollAttempts,
            ExportPollDelaySeconds = exportPollDelaySeconds,
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var enebaHttp = new EnebaHttpClient(httpClient, options, cache, NullLogger<EnebaHttpClient>.Instance);
        var provider = new EnebaSupplierProvider(enebaHttp, options, NullLogger<EnebaSupplierProvider>.Instance);
        return (provider, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage DefaultTokenResponse(string accessToken = "fake-access-token", int expiresIn = 600) =>
        JsonResponse(HttpStatusCode.OK, new { access_token = accessToken, expires_in = expiresIn, token_type = "Bearer" });

    private static HttpResponseMessage GraphQlData(object data) => JsonResponse(HttpStatusCode.OK, new { data });

    private static HttpResponseMessage GraphQlErrors(params string[] messages) =>
        JsonResponse(HttpStatusCode.OK, new { data = (object?)null, errors = messages.Select(m => new { message = m }).ToArray() });

    private static object WholesaleAuctionsData(params (string Id, string ProductName, string Merchant, int AmountCents, string Currency, int? Stock)[] auctions) => new
    {
        P_wholesaleAuctions = new
        {
            totalCount = auctions.Length,
            pageInfo = new { hasNextPage = false, hasPreviousPage = false, startCursor = (string?)null, endCursor = auctions.Length > 0 ? "cursor-1" : null },
            edges = auctions.Select(a => new
            {
                cursor = "cursor-1",
                node = new
                {
                    id = a.Id,
                    wholesalePrice = new { amount = a.AmountCents, currency = a.Currency },
                    wholesaleStock = a.Stock,
                    merchant = new { displayName = a.Merchant, slug = (string?)null },
                    product = new { id = (string?)null, name = a.ProductName, slug = (string?)null, productType = (string?)null },
                },
            }).ToArray(),
        },
    };

    private static object PurchaseData(bool success, string? orderId, string? actionId) => new
    {
        S_purchaseWholesaleAuctions = new { success, orderId, actionId },
    };

    private static object OrdersData(string orderId, string orderNumber, string entryToken, string orderState, params (string ShortId, string SellableSlug, int Quantity)[] items) => new
    {
        O_orders = new
        {
            edges = new[]
            {
                new
                {
                    node = new
                    {
                        id = orderId,
                        orderNumber,
                        entryToken,
                        orderState,
                        paymentState = "PAID",
                        createdAt = DateTimeOffset.UtcNow,
                        items = items.Select(i => new { shortId = i.ShortId, sellableName = (string?)"Test Item", sellableSlug = i.SellableSlug, quantity = i.Quantity }).ToArray(),
                    },
                },
            },
        },
    };

    private static object ExportOrderKeysData(bool success) => new { O_exportOrderKeys = new { success } };

    private static object OrderExportData(string? status, string? downloadUrl) => new { O_orderExport = new { status, downloadUrl } };

    private static byte[] BuildArchive(string orderNumber, string sellableSlug, string shortId, string? keysTxtContent, IEnumerable<string>? imageFileNames = null)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            var prefix = $"{orderNumber}/{sellableSlug}/{shortId}/";
            if (keysTxtContent is not null)
            {
                var entry = zip.CreateEntry(prefix + "keys.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(keysTxtContent);
            }

            foreach (var name in imageFileNames ?? [])
            {
                var entry = zip.CreateEntry(prefix + name);
                using var stream = entry.Open();
                stream.WriteByte(0);
            }
        }

        // SharpCompress reads a Password option only against entries actually flagged encrypted — a plain
        // ZipArchive-written (unencrypted) test fixture is read back correctly regardless of the password
        // passed, so this validates EnebaArchiveReader's path-matching/keys.txt parsing logic without also
        // re-verifying SharpCompress's own ZipCrypto decryption (a vetted third-party concern, not this
        // integration's code).
        return memory.ToArray();
    }

    private const string SearchOperation = "P_wholesaleAuctions";
    private const string PurchaseOperation = "S_purchaseWholesaleAuctions";
    private const string OrdersOperation = "O_orders";
    private const string ExportOperation = "O_exportOrderKeys";
    private const string OrderExportOperation = "O_orderExport";

    // ===================== Authentication =====================

    [Fact]
    public async Task TestConnectionAsync_AcquiresToken_AndSendsBearerHeaderOnGraphQlCall()
    {
        string? capturedAuth = null;
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) =>
        {
            capturedAuth = req.Headers.Authorization?.Parameter;
            return Task.FromResult(GraphQlData(WholesaleAuctionsData()));
        });

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("fake-access-token", capturedAuth);
    }

    [Fact]
    public async Task TestConnectionAsync_TokenRejected_IsReportedAsUnsuccessful_WithSafeMessage()
    {
        var (provider, _) = CreateProvider(
            tokenResponder: (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("fake-auth-secret", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingOAuthSettings_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, body, ct) => throw new InvalidOperationException("must never reach GraphQL without credentials"));
        var contextWithNoCredentials = new SupplierProviderContext(
            Guid.NewGuid(), "ENEBA-1", null, new SupplierProviderCredentials(null, null, null, null, null, null), SettingsJson: null);

        var result = await provider.TestConnectionAsync(contextWithNoCredentials);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ExpiredToken_Real401OnGraphQlCall_RefreshesTokenOnce_AndRetries()
    {
        var tokenCalls = 0;
        var graphQlCalls = 0;
        var (provider, _) = CreateProvider(
            tokenResponder: (req, ct) =>
            {
                tokenCalls++;
                return Task.FromResult(DefaultTokenResponse(accessToken: $"token-{tokenCalls}"));
            },
            graphQlResponder: (req, body, ct) =>
            {
                graphQlCalls++;
                return Task.FromResult(graphQlCalls == 1
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : GraphQlData(WholesaleAuctionsData()));
            });

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, tokenCalls);
        Assert.Equal(2, graphQlCalls);
    }

    // ===================== Catalog retrieval =====================

    [Fact]
    public async Task SearchCatalogAsync_MapsAuctionIdAsExternalProductId()
    {
        var (provider, handler) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(GraphQlData(WholesaleAuctionsData(
            ("11111111-1111-1111-1111-111111111111", "Test Game Key", "SomeMerchant", 1999, "EUR", 5)))));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("test", 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal("11111111-1111-1111-1111-111111111111", item.ExternalProductId);
        Assert.Equal("SomeMerchant", item.BrandName);
        Assert.Equal(19.99m, item.MinFaceValue);
        Assert.True(item.Available);
        Assert.Contains("\"search\":\"test\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task SearchCatalogAsync_ZeroStock_IsUnavailable()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(GraphQlData(WholesaleAuctionsData(
            ("11111111-1111-1111-1111-111111111111", "Test Game Key", "SomeMerchant", 1999, "EUR", 0)))));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.False(result.Items.Single().Available);
    }

    [Fact]
    public async Task SearchCatalogAsync_GraphQlErrors_IsReportedAsUnsuccessful()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(GraphQlErrors("not authorized")));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    // ===================== Availability (documented gap) =====================

    [Fact]
    public async Task GetAvailabilityAsync_NeverInvented_ReturnsUnsuccessful_WithExplanation()
    {
        var (provider, _) = CreateProvider((req, body, ct) => throw new InvalidOperationException("must never call GraphQL — no batch-by-id lookup exists"));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["11111111-1111-1111-1111-111111111111"]), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
        Assert.NotNull(result.Message);
    }

    // ===================== Purchase =====================

    [Fact]
    public async Task PurchaseAsync_Success_ReturnsOrderId_WithNullDeliveredCodes_AsynchronousContract()
    {
        var (provider, handler) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(
            GraphQlData(PurchaseData(true, "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333"))));

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("22222222-2222-2222-2222-222222222222", result.ProviderOrderId);
        Assert.Null(result.DeliveredCodes);
        Assert.Contains("\"auctionId\":\"11111111-1111-1111-1111-111111111111\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task PurchaseAsync_QuantityOutOfRange_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, body, ct) => throw new InvalidOperationException("must not call Eneba with an out-of-range quantity"));

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 2001, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_InvalidAuctionId_FailsClosed_WithoutAnyHttpCall()
    {
        var (provider, _) = CreateProvider((req, body, ct) => throw new InvalidOperationException("must not call Eneba with a non-UUID auction id"));

        var request = new SupplierPurchaseRequest("not-a-uuid", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_GraphQlErrors_NoOrderId_IsDefiniteFailure_NeverAmbiguous()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(GraphQlErrors("quantity must be between 1 and 2000")));

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Null(result.ProviderOrderId);
        Assert.Equal(SupplierFulfillmentFailureCategory.UnknownProviderState, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_OrderIdCaptured_TrustedEvenAlongsideGraphQlErrors_NeverLosesAnAcceptedPurchase()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            data = PurchaseData(true, "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333"),
            errors = new[] { new { message = "some unrelated partial warning" } },
        })));

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("22222222-2222-2222-2222-222222222222", result.ProviderOrderId);
    }

    [Fact]
    public async Task PurchaseAsync_RealHttpAuthFailure_IsDefiniteNotAmbiguous()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)),
            tokenResponder: (req, ct) => Task.FromResult(DefaultTokenResponse()));
        // 403 on GraphQL after a valid token is a definite authorization failure — never retried like the
        // one-time 401-refresh path (that path is for the token itself being stale, not for a genuine
        // access-denied response).

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.AuthenticationFailed, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_RateLimited_IsDefiniteProviderUnavailable()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)));

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.ProviderUnavailable, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_Timeout_ThrowsAmbiguous_NeverReturnsFailureResult()
    {
        var (provider, _) = CreateProvider(graphQlResponder: async (req, body, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return GraphQlData(PurchaseData(true, "orderid", "actionid"));
        }, timeoutSeconds: 1);

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        await Assert.ThrowsAsync<EnebaAmbiguousResponseException>(() => provider.PurchaseAsync(request, CreateContext()));
    }

    [Fact]
    public async Task PurchaseAsync_MalformedSuccessBody_NoOrderIdNoErrors_ThrowsAmbiguous_NeverTrustsEmptySuccess()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(
            GraphQlData(PurchaseData(true, null, null))));

        var request = new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR");
        await Assert.ThrowsAsync<EnebaAmbiguousResponseException>(() => provider.PurchaseAsync(request, CreateContext()));
    }

    // ===================== Order status / reconciliation =====================

    [Fact]
    public async Task GetOrderStatusAsync_NoProviderOrderId_ThrowsAmbiguous_PermanentlyUnreconcilable()
    {
        var (provider, _) = CreateProvider((req, body, ct) => throw new InvalidOperationException("must never call GraphQL — nothing to look up"));

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), null);
        await Assert.ThrowsAsync<EnebaAmbiguousResponseException>(() => provider.GetOrderStatusAsync(query, CreateContext()));
    }

    [Theory]
    [InlineData("NEW")]
    [InlineData("CART")]
    public async Task GetOrderStatusAsync_NewOrCart_IsProcessing(string orderState)
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(
            GraphQlData(OrdersData("orderid", "o-abc123", "entry-token", orderState, ("short-1", "some-slug", 1)))));

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), "orderid");
        var result = await provider.GetOrderStatusAsync(query, CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Processing, result.Status);
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("FAILED")]
    public async Task GetOrderStatusAsync_CancelledOrFailed_IsDefiniteFailure(string orderState)
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(
            GraphQlData(OrdersData("orderid", "o-abc123", "entry-token", orderState, ("short-1", "some-slug", 1)))));

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), "orderid");
        var result = await provider.GetOrderStatusAsync(query, CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Failed, result.Status);
        Assert.NotNull(result.DeliveredCodes);
        Assert.Empty(result.DeliveredCodes!);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Fulfilled_ExtractsKeysFromArchive_ReturnsSucceeded()
    {
        var archiveBytes = BuildArchive("o-abc123", "some-slug", "short-1", "KEY-AAAA-1111\nKEY-BBBB-2222");

        var (provider, _) = CreateProvider(
            graphQlResponder: (req, body, ct) =>
            {
                if (body.Contains(OrdersOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(OrdersData("orderid", "o-abc123", "entry-token", "FULFILLED", ("short-1", "some-slug", 2))));
                }

                if (body.Contains(ExportOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(ExportOrderKeysData(true)));
                }

                if (body.Contains(OrderExportOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(OrderExportData("COMPLETED", "https://cdn.eneba.example/export/archive.zip")));
                }

                throw new InvalidOperationException("Unexpected GraphQL operation: " + body);
            },
            archiveResponder: (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(archiveBytes),
            }));

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), "orderid");
        var result = await provider.GetOrderStatusAsync(query, CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Succeeded, result.Status);
        Assert.NotNull(result.DeliveredCodes);
        Assert.Equal(2, result.DeliveredCodes!.Count);
        Assert.Contains("KEY-AAAA-1111", result.DeliveredCodes);
        Assert.Contains("KEY-BBBB-2222", result.DeliveredCodes);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Fulfilled_ExportNotReadyWithinPollBudget_ReturnsProcessing_NotFailure()
    {
        var (provider, _) = CreateProvider(
            exportPollAttempts: 2,
            graphQlResponder: (req, body, ct) =>
            {
                if (body.Contains(OrdersOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(OrdersData("orderid", "o-abc123", "entry-token", "FULFILLED", ("short-1", "some-slug", 1))));
                }

                if (body.Contains(ExportOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(ExportOrderKeysData(true)));
                }

                if (body.Contains(OrderExportOperation, StringComparison.Ordinal))
                {
                    // Never completes within this reconciliation tick's poll budget.
                    return Task.FromResult(GraphQlData(OrderExportData("PROCESSING", null)));
                }

                throw new InvalidOperationException("Unexpected GraphQL operation: " + body);
            });

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), "orderid");
        var result = await provider.GetOrderStatusAsync(query, CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Processing, result.Status);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Fulfilled_ImageKeysOnly_IsDefiniteFailure_NeverGuessesText()
    {
        var archiveBytes = BuildArchive("o-abc123", "some-slug", "short-1", keysTxtContent: null, imageFileNames: ["key-1.png"]);

        var (provider, _) = CreateProvider(
            graphQlResponder: (req, body, ct) =>
            {
                if (body.Contains(OrdersOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(OrdersData("orderid", "o-abc123", "entry-token", "FULFILLED", ("short-1", "some-slug", 1))));
                }

                if (body.Contains(ExportOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(ExportOrderKeysData(true)));
                }

                if (body.Contains(OrderExportOperation, StringComparison.Ordinal))
                {
                    return Task.FromResult(GraphQlData(OrderExportData("COMPLETED", "https://cdn.eneba.example/export/archive.zip")));
                }

                throw new InvalidOperationException("Unexpected GraphQL operation: " + body);
            },
            archiveResponder: (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archiveBytes) }));

        var query = new SupplierOrderStatusQuery(Guid.NewGuid(), "orderid");
        var result = await provider.GetOrderStatusAsync(query, CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Failed, result.Status);
        Assert.NotNull(result.Message);
    }

    // ===================== Secret-leak regression =====================

    [Fact]
    public async Task NoResultMessage_EverContainsAuthSecretOrAccessToken()
    {
        var (provider, _) = CreateProvider(graphQlResponder: (req, body, ct) => Task.FromResult(GraphQlErrors("Some Eneba-side validation error")));

        var connectionResult = await provider.TestConnectionAsync(CreateContext());
        var purchaseResult = await provider.PurchaseAsync(
            new SupplierPurchaseRequest("11111111-1111-1111-1111-111111111111", 1, null, Guid.NewGuid().ToString(), 19.99m, "EUR"), CreateContext());

        Assert.DoesNotContain("fake-auth-secret", connectionResult.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fake-access-token", connectionResult.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fake-auth-secret", purchaseResult.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fake-access-token", purchaseResult.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
