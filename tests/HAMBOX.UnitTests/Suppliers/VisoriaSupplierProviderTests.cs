using System.Net;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Everything here talks to a fake <see cref="HttpMessageHandler"/> — never a real network call, never
/// real Visoria credentials. Covers <see cref="VisoriaSupplierProvider"/>'s mapping of Visoria's
/// documented request/response shapes (docs/integrations/suppliers/api-2.json) into the generic
/// <see cref="ISupplierProvider"/> contract. Mirrors <c>BambooSupplierProviderTests</c>'s structure.
/// </summary>
public sealed class VisoriaSupplierProviderTests
{
    private static readonly SupplierProviderCredentials Credentials = new(null, null, null, null, "vsk_test_fake-token", null);

    private static SupplierProviderContext CreateContext() =>
        new(Guid.NewGuid(), "VISORIA-1", null, Credentials, SettingsJson: null);

    private static (VisoriaSupplierProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        int timeoutSeconds = 15,
        ILogger<VisoriaSupplierProvider>? logger = null)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(VisoriaProviderConstants.BaseUrl) };
        var options = Options.Create(new VisoriaProviderOptions { RequestTimeoutSeconds = timeoutSeconds, MaxResponseBytes = 1024 * 1024 });
        var visoriaHttp = new VisoriaHttpClient(httpClient, options, NullLogger<VisoriaHttpClient>.Instance);
        var provider = new VisoriaSupplierProvider(visoriaHttp, logger ?? NullLogger<VisoriaSupplierProvider>.Instance);
        return (provider, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static SupplierPurchaseRequest CreatePurchaseRequest(int quantity = 1, string? currency = "USD") =>
        new("prod_steam_50", quantity, null, Guid.NewGuid().ToString(), UnitFaceValue: null, Currency: currency);

    private static object BasicProduct(string id = "prod_steam_50", bool orderable = true, string fulfillmentType = "PIN") => new
    {
        id,
        name = "Steam Wallet $50",
        categories = new[] { new { id = "cat_gaming", name = "Gaming" } },
        market_price = 50m,
        currency_code = "USD",
        denomination = new { type = "BASIC", min = 1m, max = 1m },
        orderable,
        stock = 0,
        stock_unlimited = true,
        fulfillment_type = fulfillmentType,
    };

    // Every purchase first resolves the live product (fulfillment_type/denomination) before creating
    // the order — the fake handler dispatches by path so both calls can be scripted in one test.
    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Router(
        object product, Func<HttpRequestMessage, HttpResponseMessage> onCreateOrder) =>
        (req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/orders"))
            {
                return Task.FromResult(onCreateOrder(req));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, product));
        };

    // A. Successful synchronous Visoria purchase (COMPLETED with delivered keys)
    [Fact]
    public async Task PurchaseAsync_CompletedWithDeliveredKeys_ReturnsSucceededCodes()
    {
        var order = new
        {
            id = "ord_7f3a2b1c",
            number = "ORD-2026-0042",
            status = "COMPLETED",
            items = new[]
            {
                new
                {
                    product_id = "prod_steam_50",
                    quantity = 1,
                    keys = new[] { new { id = "key_abc123", status = "DELIVERED", key = "XXXX-YYYY-ZZZZ", pin = (string?)null } },
                },
            },
        };

        var (provider, handler) = CreateProvider(Router(BasicProduct(), _ => JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("ord_7f3a2b1c", result.ProviderOrderId);
        Assert.NotNull(result.DeliveredCodes);
        Assert.Equal("XXXX-YYYY-ZZZZ", result.DeliveredCodes!.Single());
        Assert.Contains("Idempotency-Key", handler.LastRequest!.Headers.Select(h => h.Key));
    }

    [Fact]
    public async Task PurchaseAsync_Progressing_ReturnsSubmittedWithNoCodesYet()
    {
        var order = new { id = "ord_1", number = "ORD-1", status = "PROGRESSING", items = Array.Empty<object>() };
        var (provider, _) = CreateProvider(Router(BasicProduct(), _ => JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("ord_1", result.ProviderOrderId);
        Assert.Null(result.DeliveredCodes); // outcome not yet known — must reconcile
    }

    [Fact]
    public async Task PurchaseAsync_OpenDenomination_SendsConfiguredFaceValue()
    {
        var openProduct = new
        {
            id = "prod_open_1",
            name = "Open Top-Up",
            categories = Array.Empty<object>(),
            market_price = 0m,
            currency_code = "USD",
            denomination = new { type = "OPEN", min = 5m, max = 100m },
            orderable = true,
            stock = 0,
            stock_unlimited = true,
            fulfillment_type = "PIN",
        };
        var order = new { id = "ord_2", number = "ORD-2", status = "PROGRESSING", items = Array.Empty<object>() };

        string? capturedBody = null;
        var (provider, _) = CreateProvider((req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/orders"))
            {
                capturedBody = req.Content!.ReadAsStringAsync(ct).Result;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, order));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, openProduct));
        });

        var request = new SupplierPurchaseRequest("prod_open_1", 1, null, Guid.NewGuid().ToString(), UnitFaceValue: 30m, Currency: "USD");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Contains("\"face_value\":30", capturedBody);
    }

    [Fact]
    public async Task PurchaseAsync_OpenDenomination_MissingFaceValue_FailsClosed_WithoutOrderCall()
    {
        var openProduct = new
        {
            id = "prod_open_1",
            name = "Open Top-Up",
            categories = Array.Empty<object>(),
            market_price = 0m,
            currency_code = "USD",
            denomination = new { type = "OPEN", min = 5m, max = 100m },
            orderable = true,
            stock = 0,
            stock_unlimited = true,
            fulfillment_type = "PIN",
        };

        var (provider, _) = CreateProvider((req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/orders"))
            {
                throw new InvalidOperationException("Must not create an order without a resolved face value.");
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, openProduct));
        });

        var request = new SupplierPurchaseRequest("prod_open_1", 1, null, Guid.NewGuid().ToString(), UnitFaceValue: null, Currency: "USD");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_BasicDenomination_AlwaysSendsFaceValueOne()
    {
        var order = new { id = "ord_3", number = "ORD-3", status = "PROGRESSING", items = Array.Empty<object>() };
        string? capturedBody = null;
        var (provider, _) = CreateProvider((req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/orders"))
            {
                capturedBody = req.Content!.ReadAsStringAsync(ct).Result;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, order));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, BasicProduct()));
        });

        // Deliberately configure an unrelated BuyingPrice-derived face value — must be ignored for
        // non-OPEN products, since Visoria rejects anything but exactly 1.
        var request = new SupplierPurchaseRequest("prod_steam_50", 1, null, Guid.NewGuid().ToString(), UnitFaceValue: 47.5m, Currency: "USD");
        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Contains("\"face_value\":1", capturedBody);
    }

    // Genuine, documented HAMBOX capability gap — recharge products need per-unit customer account
    // data (recharge_data) that SupplierPurchaseRequest has no field for. Must fail closed, never
    // attempt an order Visoria would reject anyway.
    [Fact]
    public async Task PurchaseAsync_RechargeProduct_FailsClosed_WithoutOrderCall()
    {
        var (provider, _) = CreateProvider((req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/orders"))
            {
                throw new InvalidOperationException("Must never submit an order for an unsupported recharge product.");
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, BasicProduct(fulfillmentType: "RECHARGE")));
        });

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidProduct, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_ProductNotOrderable_FailsClosed()
    {
        var (provider, _) = CreateProvider(Router(BasicProduct(orderable: false), _ => throw new InvalidOperationException("must not reach order creation")));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.ProductUnavailable, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_MissingCurrency_FailsClosed_WithoutAnyHttpCall()
    {
        var handler = new FakeHttpMessageHandler((req, ct) => throw new InvalidOperationException("must not call Visoria without a currency"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(VisoriaProviderConstants.BaseUrl) };
        var visoriaHttp = new VisoriaHttpClient(httpClient, Options.Create(new VisoriaProviderOptions()), NullLogger<VisoriaHttpClient>.Instance);
        var provider = new VisoriaSupplierProvider(visoriaHttp, NullLogger<VisoriaSupplierProvider>.Instance);

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(currency: null), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_ProductNotFound_IsDefiniteInvalidProduct()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"code":"REQUESTx03","message":"The requested item could not be found"}""", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidProduct, result.FailureCategory);
    }

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

    [Fact]
    public async Task PurchaseAsync_ValidationError_IsDefiniteUnknownProviderState_NeverGuessed()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("""{"code":"VALIDATORx01","message":"Please check your input and try again"}""", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.UnknownProviderState, result.FailureCategory);
    }

    [Fact]
    public async Task PurchaseAsync_RateLimited_IsDefiniteProviderUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("""{"code":"REQUESTx01","message":"You have made too many requests"}""", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.ProviderUnavailable, result.FailureCategory);
    }

    // C. Timeout -> must throw (ambiguous), never return a definite failure
    [Fact]
    public async Task PurchaseAsync_Timeout_ThrowsAmbiguous_NeverReturnsFailureResult()
    {
        var (provider, _) = CreateProvider(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct); // outlasts the 1s client timeout below
            return JsonResponse(HttpStatusCode.OK, BasicProduct());
        }, timeoutSeconds: 1);

        await Assert.ThrowsAsync<VisoriaAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task PurchaseAsync_ServerError_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain"),
        }));

        await Assert.ThrowsAsync<VisoriaAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task PurchaseAsync_MalformedJsonBody_ThrowsAmbiguous()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-valid-json", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<VisoriaAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    /// <summary>
    /// Unlike Bamboo, Visoria's own idempotency-key reuse never returns an ambiguous "already exists"
    /// error — a duplicate Idempotency-Key just returns the SAME order with a normal 200. This proves
    /// a retried purchase attempt safely converges instead of double-purchasing, without needing any
    /// special ambiguous-response mapping.
    /// </summary>
    [Fact]
    public async Task PurchaseAsync_DuplicateIdempotencyKey_ReturnsSameOrder_NeverThrows()
    {
        var order = new
        {
            id = "ord_existing",
            number = "ORD-2026-0042",
            status = "COMPLETED",
            items = new[]
            {
                new { product_id = "prod_steam_50", quantity = 1, keys = new[] { new { id = "key_1", status = "DELIVERED", key = "CODE-1", pin = (string?)null } } },
            },
        };

        var (provider, _) = CreateProvider(Router(BasicProduct(), _ => JsonResponse(HttpStatusCode.OK, order)));

        var request = CreatePurchaseRequest();
        var first = await provider.PurchaseAsync(request, CreateContext());
        var second = await provider.PurchaseAsync(request, CreateContext());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.ProviderOrderId, second.ProviderOrderId);
        Assert.Equal(first.DeliveredCodes!.Single(), second.DeliveredCodes!.Single());
    }

    // ===================== GetOrderStatusAsync =====================

    [Fact]
    public async Task GetOrderStatusAsync_Completed_FullQuantityDelivered_ReturnsSucceeded()
    {
        var order = new
        {
            id = "ord_1",
            number = "ORD-1",
            status = "COMPLETED",
            items = new[]
            {
                new
                {
                    product_id = "prod_steam_50",
                    quantity = 2,
                    keys = new[]
                    {
                        new { id = "k1", status = "DELIVERED", key = (string?)"CODE-1", pin = (string?)null },
                        new { id = "k2", status = "DELIVERED", key = (string?)"CODE-2", pin = (string?)"9999" },
                    },
                },
            },
        };

        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "ord_1"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Succeeded, result.Status);
        Assert.Equal(2, result.DeliveredCodes!.Count);
        Assert.Contains("CODE-1", result.DeliveredCodes);
        Assert.Contains("CODE-2:9999", result.DeliveredCodes);
        // Always looked up by HamboxReferenceId (idempotency key), never by ProviderOrderId.
        Assert.Contains("by-idempotency-key", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Completed_PartialQuantityDelivered_ReturnsPartialFailed()
    {
        var order = new
        {
            id = "ord_2",
            number = "ORD-2",
            status = "COMPLETED",
            items = new[]
            {
                new
                {
                    product_id = "prod_steam_50",
                    quantity = 2,
                    keys = new[]
                    {
                        new { id = "k1", status = "DELIVERED", key = (string?)"CODE-1", pin = (string?)null },
                        new { id = "k2", status = "RESERVED", key = (string?)null, pin = (string?)null },
                    },
                },
            },
        };

        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), null), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.PartialFailed, result.Status);
        Assert.Single(result.DeliveredCodes!);
        Assert.Equal("CODE-1", result.DeliveredCodes!.Single());
    }

    [Fact]
    public async Task GetOrderStatusAsync_Cancelled_ReturnsFailed()
    {
        var order = new { id = "ord_3", number = "ORD-3", status = "CANCELLED", items = Array.Empty<object>() };
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), null), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Failed, result.Status);
        Assert.Empty(result.DeliveredCodes!);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Progressing_ReturnsProcessing_NullCodes()
    {
        var order = new { id = "ord_4", number = "ORD-4", status = "PROGRESSING", items = Array.Empty<object>() };
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), null), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Processing, result.Status);
        Assert.Null(result.DeliveredCodes);
    }

    [Fact]
    public async Task GetOrderStatusAsync_KeyDeliveredButNoValue_MissingKeysReadScope_ExcludedNotTrusted()
    {
        var order = new
        {
            id = "ord_5",
            number = "ORD-5",
            status = "COMPLETED",
            items = new[]
            {
                new
                {
                    product_id = "prod_steam_50",
                    quantity = 1,
                    keys = new[] { new { id = "k1", status = "DELIVERED", key = (string?)null, pin = (string?)null } },
                },
            },
        };

        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, order)));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), null), CreateContext());

        // A DELIVERED key with no value (missing keys:read scope) must never be treated as a usable code.
        Assert.Equal(SupplierProviderOrderStatus.Failed, result.Status);
        Assert.Empty(result.DeliveredCodes!);
    }

    // ===================== GetAvailabilityAsync =====================

    [Fact]
    public async Task GetAvailabilityAsync_OrderableWithStock_ReturnsAvailable()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            data = new[] { new { id = "prod_1", name = "P1", categories = Array.Empty<object>(), market_price = 10m, currency_code = "USD", denomination = new { type = "BASIC", min = 1m, max = 1m }, orderable = true, stock = 5, stock_unlimited = false, fulfillment_type = "PIN" } },
        })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["prod_1"]), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal(SupplierAvailabilityState.Available, item.State);
        Assert.Equal(5, item.AvailableQuantity);
        Assert.Contains("page=1", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task GetAvailabilityAsync_RechargeProduct_ReportsUnavailable_EvenIfOrderable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            data = new[] { new { id = "prod_recharge", name = "Top-up", categories = Array.Empty<object>(), market_price = 0m, currency_code = "USD", denomination = new { type = "OPEN", min = 1m, max = 100m }, orderable = true, stock = 0, stock_unlimited = true, fulfillment_type = "RECHARGE" } },
        })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["prod_recharge"]), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single().State);
    }

    [Fact]
    public async Task GetAvailabilityAsync_RequestedIdNotInCatalog_ReturnsUnavailable_NotUnknown()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { data = Array.Empty<object>() })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["does-not-exist"]), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single().State);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ProviderCallFails_ReturnsUnsuccessful_NeverFabricatesUnavailable()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["prod_1"]), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAvailabilityAsync_MissingCredentials_ReturnsUnsuccessful()
    {
        var (provider, _) = CreateProvider((req, ct) => throw new InvalidOperationException("must never reach the network without credentials"));
        var contextWithNoCredentials = new SupplierProviderContext(
            Guid.NewGuid(), "VISORIA-1", null, new SupplierProviderCredentials(null, null, null, null, null, null), SettingsJson: null);

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["prod_1"]), contextWithNoCredentials);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    // ===================== SearchCatalogAsync =====================

    [Fact]
    public async Task SearchCatalogAsync_FiltersClientSideByName_NoServerSearchParamExists()
    {
        var (provider, handler) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            data = new[]
            {
                new { id = "prod_steam_50", name = "Steam Wallet $50", categories = new[] { new { id = "cat_gaming", name = "Gaming" } }, market_price = 50m, currency_code = "USD", denomination = new { type = "BASIC", min = 1m, max = 1m }, orderable = true, stock = 0, stock_unlimited = true, fulfillment_type = "PIN" },
                new { id = "prod_xbox_25", name = "Xbox Gift Card $25", categories = new[] { new { id = "cat_gaming", name = "Gaming" } }, market_price = 25m, currency_code = "USD", denomination = new { type = "BASIC", min = 1m, max = 1m }, orderable = true, stock = 0, stock_unlimited = true, fulfillment_type = "PIN" },
            },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("steam", 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal("prod_steam_50", item.ExternalProductId);
        Assert.Equal(50m, item.MinFaceValue); // BASIC -> market_price, not denomination.min/max (always 1)
        Assert.Equal(50m, item.MaxFaceValue);
        // No text-search query parameter is sent — filtering happens client-side in this adapter.
        Assert.DoesNotContain("search", handler.LastRequest!.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchCatalogAsync_OpenDenomination_UsesRealMinMax()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            data = new[] { new { id = "prod_open", name = "Open Top-Up", categories = Array.Empty<object>(), market_price = 0m, currency_code = "USD", denomination = new { type = "OPEN", min = 5m, max = 100m }, orderable = true, stock = 0, stock_unlimited = true, fulfillment_type = "PIN" } },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        var item = Assert.Single(result.Items);
        Assert.Equal(5m, item.MinFaceValue);
        Assert.Equal(100m, item.MaxFaceValue);
    }

    [Fact]
    public async Task SearchCatalogAsync_ExcludesRechargeProducts_NeverOfferedForMapping()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            data = new[]
            {
                new { id = "prod_pin", name = "Steam Wallet", categories = Array.Empty<object>(), market_price = 10m, currency_code = "USD", denomination = new { type = "BASIC", min = 1m, max = 1m }, orderable = true, stock = 0, stock_unlimited = true, fulfillment_type = "PIN" },
                new { id = "prod_recharge", name = "Mobile Recharge", categories = Array.Empty<object>(), market_price = 0m, currency_code = "USD", denomination = new { type = "OPEN", min = 1m, max = 50m }, orderable = true, stock = 0, stock_unlimited = true, fulfillment_type = "RECHARGE" },
            },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Items);
        Assert.Equal("prod_pin", result.Items.Single().ExternalProductId);
    }

    [Fact]
    public async Task SearchCatalogAsync_EmptyItems_ReturnsEmptySuccessNotFailure()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { data = Array.Empty<object>() })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("nonexistent", 1, 20), CreateContext());

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
        Assert.DoesNotContain("vsk_test_", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== TestConnectionAsync =====================

    [Fact]
    public async Task TestConnectionAsync_Success_SummarizesSafeFacts_NeverBalanceAmount()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new[]
        {
            new { balance = 1250.5m, currency_code = "USD", last_updated_at = "2026-06-10T12:00:00.000Z", livemode = false },
        })));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Contains("USD", result.Message);
        Assert.Contains("test", result.Message);
        Assert.DoesNotContain("1250.5", result.Message);
        Assert.DoesNotContain("vsk_test_", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectionAsync_NoBalances_ReportsZero_NeverThrows()
    {
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, Array.Empty<object>())));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Contains("0 currency balance", result.Message);
    }

    // T. Credentials/redemption secrets never appear in logs, across every path exercised above.
    [Fact]
    public async Task Provider_NeverLogsCredentialsOrDeliveredCodes()
    {
        var recordingLogger = new RecordingLogger<VisoriaSupplierProvider>();
        var (provider, _) = CreateProvider((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }), logger: recordingLogger);

        await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        foreach (var message in recordingLogger.Messages)
        {
            Assert.DoesNotContain("vsk_test_", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bearer ", message, StringComparison.Ordinal);
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
