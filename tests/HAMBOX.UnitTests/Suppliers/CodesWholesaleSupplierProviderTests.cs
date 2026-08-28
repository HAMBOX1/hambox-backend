using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Everything here talks to a fake <see cref="HttpMessageHandler"/> — never a real network call, never
/// real CodesWholesale credentials. Covers <see cref="CodesWholesaleSupplierProvider"/>'s mapping of
/// CodesWholesale's documented v2 REST shapes (confirmed against the official PHP SDK's source — see
/// docs/integrations/suppliers/README.md) into the generic <see cref="ISupplierProvider"/> contract,
/// including the OAuth2 client-credentials token flow. Mirrors <c>EnebaSupplierProviderTests</c>'s
/// structure (the other OAuth2-based provider).
/// </summary>
public sealed class CodesWholesaleSupplierProviderTests
{
    private const string FakeClientId = "fake-client-id";
    private const string FakeClientSecret = "SUPER-SECRET-VALUE";
    private const string FakeAccessToken = "fake-access-token-abc123";

    private static readonly SupplierProviderCredentials Credentials = new(FakeClientId, FakeClientSecret, null, null, null, null);

    private static SupplierProviderContext CreateContext(string? settingsJson = null) =>
        new(Guid.NewGuid(), "CODESWHOLESALE-1", null, Credentials, settingsJson);

    private static (CodesWholesaleSupplierProvider Provider, FakeHttpMessageHandler Handler, ListLogger<CodesWholesaleSupplierProvider> Logger) CreateProvider(
        Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>>? apiResponder = null,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? tokenResponder = null,
        int timeoutSeconds = 15)
    {
        var handler = new FakeHttpMessageHandler(async (req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath == CodesWholesaleProviderConstants.OAuthTokenPath)
            {
                return tokenResponder is not null ? await tokenResponder(req, ct) : DefaultTokenResponse();
            }

            var body = req.Content is null ? string.Empty : await req.Content.ReadAsStringAsync(ct);
            return apiResponder is not null
                ? await apiResponder(req, body, ct)
                : throw new InvalidOperationException("No apiResponder configured for this test.");
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new CodesWholesaleProviderOptions
        {
            RequestTimeoutSeconds = timeoutSeconds,
            MaxResponseBytes = 1024 * 1024,
            ReconciliationLookbackDays = 7,
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpLogger = new ListLogger<CodesWholesaleHttpClient>();
        var providerLogger = new ListLogger<CodesWholesaleSupplierProvider>();
        var cwHttp = new CodesWholesaleHttpClient(httpClient, options, cache, httpLogger);
        var provider = new CodesWholesaleSupplierProvider(cwHttp, cache, providerLogger);
        return (provider, handler, providerLogger);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage DefaultTokenResponse(string accessToken = FakeAccessToken, int expiresIn = 3600) =>
        JsonResponse(HttpStatusCode.OK, new { access_token = accessToken, expires_in = expiresIn, token_type = "bearer" });

    private static object TextCode(string codeId, string code) => new { codeId, status = CodesWholesaleProviderConstants.CodeStatusText, code };
    private static object PreOrderCode(string codeId) => new { codeId, status = CodesWholesaleProviderConstants.CodeStatusPreOrder, code = (string?)null };
    private static object ImageCode(string codeId) => new { codeId, status = CodesWholesaleProviderConstants.CodeStatusImage, code = "base64data", filename = "code.png" };

    private static object OrderResponse(string orderId, string? clientOrderId, decimal totalPrice, string productId, params object[] codes) => new
    {
        orderId,
        clientOrderId,
        totalPrice,
        status = "REALIZED",
        createdOn = "2026-01-01T00:00:00Z",
        products = new[] { new { productId, unitPrice = totalPrice, codes } },
    };

    private static SupplierPurchaseRequest CreatePurchaseRequest(int quantity = 1, string? externalProductId = "6313677f-5219-47e4-a067-7401f55c5a3a", string? referenceId = null) =>
        new(externalProductId!, quantity, null, referenceId ?? Guid.NewGuid().ToString(), UnitFaceValue: null, Currency: null);

    // ===================== Authentication =====================

    [Fact]
    public async Task TestConnection_Success_AcquiresTokenAndSendsBearerHeader()
    {
        var accountCall = false;
        var (provider, handler, _) = CreateProvider(apiResponder: (req, body, ct) =>
        {
            accountCall = true;
            Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
            Assert.Equal(FakeAccessToken, req.Headers.Authorization.Parameter);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, new { fullName = "Test Buyer", email = "buyer@example.com" }));
        });

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.True(accountCall);
        Assert.Contains("buyer@example.com", result.Message);
    }

    [Fact]
    public async Task TestConnection_SendsClientCredentialsGrant_ToTokenEndpoint()
    {
        string? capturedBody = null;
        var (provider, handler, _) = CreateProvider(
            apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { fullName = "x", email = "x@x.com" })),
            tokenResponder: async (req, ct) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync(ct);
                return DefaultTokenResponse();
            });

        await provider.TestConnectionAsync(CreateContext());

        Assert.Contains("grant_type=client_credentials", capturedBody);
        Assert.Contains($"client_id={FakeClientId}", capturedBody);
        Assert.Contains("scope=administration", capturedBody);
    }

    [Fact]
    public async Task TestConnection_InvalidClientCredentials_ReturnsFailure_NotThrows()
    {
        var (provider, _, _) = CreateProvider(
            apiResponder: (req, body, ct) => throw new InvalidOperationException("Should never reach the business API without a token."),
            tokenResponder: (req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new { error = "invalid_client", error_description = "Bad client credentials" })));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Contains("Bad client credentials", result.Message);
    }

    [Fact]
    public async Task TestConnection_MalformedTokenResponse_ReturnsFailure_NotThrows()
    {
        var (provider, _, _) = CreateProvider(tokenResponder: (req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { foo = "bar" })));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TestConnection_NoCredentialsConfigured_ReturnsFailure_WithoutCallingApi()
    {
        var (provider, handler, _) = CreateProvider();
        var context = new SupplierProviderContext(Guid.NewGuid(), "CW-1", null, new SupplierProviderCredentials(null, null, null, null, null, null), null);

        var result = await provider.TestConnectionAsync(context);

        Assert.False(result.IsSuccess);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Token_IsCached_SecondCallDoesNotRequestNewToken()
    {
        var tokenCalls = 0;
        var (provider, _, _) = CreateProvider(
            apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { fullName = "x", email = "x@x.com" })),
            tokenResponder: (req, ct) => { tokenCalls++; return Task.FromResult(DefaultTokenResponse()); });

        var context = CreateContext();
        await provider.TestConnectionAsync(context);
        await provider.TestConnectionAsync(context);

        Assert.Equal(1, tokenCalls);
    }

    [Fact]
    public async Task BusinessCall_401_RefreshesTokenAndRetriesOnce()
    {
        var accountCalls = 0;
        var tokenCalls = 0;
        var (provider, _, _) = CreateProvider(
            apiResponder: (req, body, ct) =>
            {
                accountCalls++;
                return Task.FromResult(accountCalls == 1
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : JsonResponse(HttpStatusCode.OK, new { fullName = "x", email = "x@x.com" }));
            },
            tokenResponder: (req, ct) => { tokenCalls++; return Task.FromResult(DefaultTokenResponse()); });

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, accountCalls);
        Assert.Equal(2, tokenCalls);
    }

    // ===================== Catalog =====================

    [Fact]
    public async Task SearchCatalog_Success_ReturnsMappedItems()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            items = new[]
            {
                new { productId = "p1", name = "Steam Wallet Code", identifier = "steam-wallet", platform = "Steam", quantity = 10, prices = new[] { new { price = 9.99m, priceRangeLabel = "1+", from = 1, to = (int?)null } } },
            },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal("p1", item.ExternalProductId);
        Assert.Equal("Steam Wallet Code", item.Name);
        Assert.True(item.Available);
        Assert.Equal(9.99m, item.MinFaceValue);
    }

    [Fact]
    public async Task SearchCatalog_FiltersBySearchTerm_ClientSide()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            items = new[]
            {
                new { productId = "p1", name = "Steam Wallet Code", identifier = "steam-wallet", platform = "Steam", quantity = 10, prices = Array.Empty<object>() },
                new { productId = "p2", name = "Xbox Gift Card", identifier = "xbox-gc", platform = "Xbox", quantity = 5, prices = Array.Empty<object>() },
            },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery("xbox", 1, 20), CreateContext());

        var item = Assert.Single(result.Items);
        Assert.Equal("p2", item.ExternalProductId);
    }

    [Fact]
    public async Task SearchCatalog_EmptyCatalog_ReturnsEmptySuccess()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { items = Array.Empty<object>() })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchCatalog_ProductMissingProductId_IsSkipped()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            items = new[] { new { productId = (string?)null, name = "Broken entry", identifier = "x", platform = "Steam", quantity = 1, prices = Array.Empty<object>() } },
        })));

        var result = await provider.SearchCatalogAsync(new SupplierCatalogQuery(null, 1, 20), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAvailability_ZeroQuantity_ReturnsUnavailable()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            items = new[] { new { productId = "p1", name = "x", identifier = "x", platform = "Steam", quantity = 0, prices = Array.Empty<object>() } },
        })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["p1"]), CreateContext());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Items);
        Assert.Equal(SupplierAvailabilityState.Unavailable, item.State);
        Assert.Equal(0, item.AvailableQuantity);
    }

    [Fact]
    public async Task GetAvailability_ProductNotInResponse_ReturnsUnavailable()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { items = Array.Empty<object>() })));

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(["missing-id"]), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierAvailabilityState.Unavailable, result.Items.Single().State);
    }

    [Fact]
    public async Task GetAvailability_BatchesRequestedIdsAcrossMultipleCalls()
    {
        var callCount = 0;
        var ids = Enumerable.Range(0, 150).Select(i => $"id-{i}").ToArray();
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) =>
        {
            callCount++;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, new { items = Array.Empty<object>() }));
        });

        var result = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(ids), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, callCount); // 150 ids / 100-per-batch => 2 calls, never one call per id.
        Assert.Equal(150, result.Items.Count);
    }

    // ===================== Purchase =====================

    [Fact]
    public async Task Purchase_Success_AllTextCodesInline_ReturnsDeliveredCodes()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", TextCode("code-1", "XXXX-YYYY-ZZZZ")))));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("order-1", result.ProviderOrderId);
        Assert.Equal(["XXXX-YYYY-ZZZZ"], result.DeliveredCodes);
    }

    [Fact]
    public async Task Purchase_SendsHamboxReferenceId_AsOrderId_ForIdempotency()
    {
        string? capturedBody = null;
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) =>
        {
            capturedBody = body;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, OrderResponse("order-1", "the-ref", 9.99m, "p1", TextCode("code-1", "ABC"))));
        });

        await provider.PurchaseAsync(CreatePurchaseRequest(referenceId: "the-ref"), CreateContext());

        Assert.Contains("\"orderId\":\"the-ref\"", capturedBody);
    }

    [Fact]
    public async Task Purchase_CodeMissingInlineValue_FetchesActualCodeViaCodeEndpoint()
    {
        var codeEndpointCalled = false;
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/v2/codes/"))
            {
                codeEndpointCalled = true;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, new { codeId = "code-1", status = CodesWholesaleProviderConstants.CodeStatusText, code = "FETCHED-CODE" }));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                OrderResponse("order-1", "ref-1", 9.99m, "p1", new { codeId = "code-1", status = CodesWholesaleProviderConstants.CodeStatusText, code = (string?)null })));
        });

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(codeEndpointCalled);
        Assert.True(result.IsSuccess);
        Assert.Equal(["FETCHED-CODE"], result.DeliveredCodes);
    }

    [Fact]
    public async Task Purchase_PreOrderedCode_ReturnsAccepted_WithNullDeliveredCodes()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", PreOrderCode("code-1")))));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("order-1", result.ProviderOrderId);
        Assert.Null(result.DeliveredCodes);
    }

    [Fact]
    public async Task Purchase_DoesNotSendAllowPreOrder_True_ByDefault()
    {
        string? capturedBody = null;
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) =>
        {
            capturedBody = body;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, OrderResponse("order-1", "ref-1", 9.99m, "p1", TextCode("code-1", "ABC"))));
        });

        await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.Contains("\"allowPreOrder\":false", capturedBody);
    }

    [Fact]
    public async Task Purchase_ImageCode_ThrowsAmbiguous_NeverReportsFailure()
    {
        // Critical safety case: an image-format code means a real purchase happened (money was spent) but
        // this integration cannot store it — reporting IsSuccess=false here would risk a duplicate
        // re-purchase elsewhere. Must throw (ambiguous/manual-reconciliation), never return a result.
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", ImageCode("code-1")))));

        await Assert.ThrowsAsync<CodesWholesaleAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task Purchase_InsufficientBalance_MapsToInsufficientSupplierBalance()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new
        {
            status = 400,
            code = CodesWholesaleProviderConstants.ErrorCodeInsufficientBalance,
            message = "Not enough funds",
        })));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InsufficientSupplierBalance, result.FailureCategory);
    }

    [Fact]
    public async Task Purchase_ProductNotFound_MapsToInvalidProduct()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.NotFound, new
        {
            status = 404,
            code = CodesWholesaleProviderConstants.ErrorCodeProductNotFound,
            message = "Product excluded from price list",
        })));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidProduct, result.FailureCategory);
    }

    [Fact]
    public async Task Purchase_UnrecognizedBusinessErrorCode_MapsToUnknownProviderState()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new
        {
            status = 400,
            code = 99999,
            message = "Some undocumented error",
        })));

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.UnknownProviderState, result.FailureCategory);
    }

    [Fact]
    public async Task Purchase_Provider5xx_ThrowsAmbiguous_NeverReturnsFailure()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<CodesWholesaleAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task Purchase_MalformedSuccessResponse_NoOrderId_ThrowsAmbiguous()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { totalPrice = 1.0m })));

        await Assert.ThrowsAsync<CodesWholesaleAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task Purchase_NoCodeEntriesAtAll_ThrowsAmbiguous_NeverFalseSuccess()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new
        {
            orderId = "order-1",
            clientOrderId = "ref-1",
            totalPrice = 9.99m,
            status = "REALIZED",
            createdOn = "2026-01-01T00:00:00Z",
            products = new[] { new { productId = "p1", unitPrice = 9.99m, codes = Array.Empty<object>() } },
        })));

        await Assert.ThrowsAsync<CodesWholesaleAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task Purchase_NetworkTimeout_ThrowsAmbiguous()
    {
        var (provider, _, _) = CreateProvider(
            apiResponder: (req, body, ct) => throw new HttpRequestException("connection reset"),
            tokenResponder: (req, ct) => Task.FromResult(DefaultTokenResponse()));

        await Assert.ThrowsAsync<CodesWholesaleAmbiguousResponseException>(() => provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext()));
    }

    [Fact]
    public async Task Purchase_MissingExternalProductId_ReturnsInvalidConfiguration_WithoutCallingApi()
    {
        var (provider, handler, _) = CreateProvider();

        var result = await provider.PurchaseAsync(CreatePurchaseRequest(externalProductId: ""), CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Purchase_MissingReferenceId_ReturnsInvalidConfiguration_WithoutCallingApi()
    {
        var (provider, handler, _) = CreateProvider();
        var request = new SupplierPurchaseRequest("p1", 1, null, ReferenceId: null);

        var result = await provider.PurchaseAsync(request, CreateContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentFailureCategory.InvalidConfiguration, result.FailureCategory);
        Assert.Null(handler.LastRequest);
    }

    // ===================== Reconciliation =====================

    [Fact]
    public async Task GetOrderStatus_ByProviderOrderId_AllDelivered_ReturnsSucceeded()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", TextCode("code-1", "ABC-123")))));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "order-1"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Succeeded, result.Status);
        Assert.Equal(["ABC-123"], result.DeliveredCodes);
    }

    [Fact]
    public async Task GetOrderStatus_StillPreOrder_ReturnsProcessing_NeverFailed()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", PreOrderCode("code-1")))));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "order-1"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Processing, result.Status);
        Assert.Null(result.DeliveredCodes);
    }

    [Fact]
    public async Task GetOrderStatus_NoProviderOrderId_FindsMatchViaOrderHistory_ByClientOrderId()
    {
        var referenceId = Guid.NewGuid();
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) =>
        {
            Assert.Contains("/v2/orders?startFrom=", req.RequestUri!.PathAndQuery);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, new
            {
                items = new[]
                {
                    OrderResponse("some-other-order", "not-a-match", 1m, "p1", TextCode("c", "x")),
                    OrderResponse("order-42", referenceId.ToString(), 9.99m, "p1", TextCode("code-1", "MATCHED-CODE")),
                },
            }));
        });

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(referenceId, null), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Succeeded, result.Status);
        Assert.Equal("order-42", result.ProviderOrderId);
        Assert.Equal(["MATCHED-CODE"], result.DeliveredCodes);
    }

    [Fact]
    public async Task GetOrderStatus_NoProviderOrderId_NotFoundInHistory_ThrowsAmbiguous_NeverFailed()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { items = Array.Empty<object>() })));

        await Assert.ThrowsAsync<CodesWholesaleAmbiguousResponseException>(
            () => provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), null), CreateContext()));
    }

    [Fact]
    public async Task GetOrderStatus_UnrecognizedCodeStatus_StaysProcessing_NeverGuessedAsDelivered()
    {
        var (provider, _, _) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", new { codeId = "c1", status = "Some new future status", code = "should-not-be-trusted" }))));

        var result = await provider.GetOrderStatusAsync(new SupplierOrderStatusQuery(Guid.NewGuid(), "order-1"), CreateContext());

        Assert.Equal(SupplierProviderOrderStatus.Processing, result.Status);
        Assert.Null(result.DeliveredCodes);
    }

    // ===================== Environment (Sandbox / Production) =====================

    [Fact]
    public void ResolveBaseUrl_DefaultsToSandbox_WhenSettingsMissing()
    {
        var url = CodesWholesaleHttpClient.ResolveBaseUrl(CreateContext(settingsJson: null));

        Assert.Equal(CodesWholesaleProviderConstants.SandboxBaseUrl, url);
    }

    [Fact]
    public void ResolveBaseUrl_UsesProduction_WhenExplicitlyConfigured()
    {
        var url = CodesWholesaleHttpClient.ResolveBaseUrl(CreateContext(settingsJson: "{\"environment\":\"Production\"}"));

        Assert.Equal(CodesWholesaleProviderConstants.ProductionBaseUrl, url);
    }

    [Fact]
    public void ResolveBaseUrl_InvalidJson_FailsClosedToSandbox()
    {
        var url = CodesWholesaleHttpClient.ResolveBaseUrl(CreateContext(settingsJson: "not json"));

        Assert.Equal(CodesWholesaleProviderConstants.SandboxBaseUrl, url);
    }

    // ===================== Security: secrets/tokens never leak =====================

    [Fact]
    public async Task ClientSecret_NeverAppearsInFailureMessageOrLogs()
    {
        var (provider, _, logger) = CreateProvider(
            tokenResponder: (req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, new { error = "invalid_client", error_description = "bad creds" })));

        var result = await provider.TestConnectionAsync(CreateContext());

        Assert.DoesNotContain(FakeClientSecret, result.Message);
        Assert.DoesNotContain(FakeClientSecret, string.Join('\n', logger.Messages));
    }

    [Fact]
    public async Task AccessToken_NeverAppearsInLogs()
    {
        var (provider, _, logger) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, new { fullName = "x", email = "x@x.com" })));

        await provider.TestConnectionAsync(CreateContext());

        Assert.DoesNotContain(FakeAccessToken, string.Join('\n', logger.Messages));
    }

    [Fact]
    public async Task DeliveredLicenseKey_NeverAppearsInLogs()
    {
        const string secretCode = "SUPER-SECRET-LICENSE-KEY-999";
        var (provider, _, logger) = CreateProvider(apiResponder: (req, body, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            OrderResponse("order-1", "ref-1", 9.99m, "p1", TextCode("code-1", secretCode)))));

        await provider.PurchaseAsync(CreatePurchaseRequest(), CreateContext());

        Assert.DoesNotContain(secretCode, string.Join('\n', logger.Messages));
    }
}

/// <summary>Minimal capturing <see cref="ILogger{T}"/> test double — records every formatted message so a test can assert a secret/token/license-key value never appears in any log line.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _messages.Enqueue(formatter(state, exception));
        if (exception is not null)
        {
            _messages.Enqueue(exception.ToString());
        }
    }
}
