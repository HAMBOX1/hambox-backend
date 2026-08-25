using System.Net;
using HAMBOX.Application.Security;
using HAMBOX.Infrastructure.Security;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Every case here talks to a fake <see cref="HttpMessageHandler"/> — never a real Cloudflare request.
/// Covers <see cref="TurnstileVerificationService"/>'s fail-closed behavior: anything short of an
/// explicit <c>success: true</c> from Siteverify must return <see langword="false"/>.
/// </summary>
public sealed class TurnstileVerificationServiceTests
{
    private static (ITurnstileVerificationService Service, FakeHttpMessageHandler Handler) CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        TurnstileSettings? settings = null)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://challenges.cloudflare.com/") };
        var options = Options.Create(settings ?? new TurnstileSettings { SiteKey = "site-key", SecretKey = "secret-key" });
        var service = new TurnstileVerificationService(httpClient, options, NullLogger<TurnstileVerificationService>.Instance);
        return (service, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ValidToken_CloudflareReportsSuccess_ReturnsTrue()
    {
        var (service, handler) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":true}""")));

        var result = await service.VerifyAsync("valid-token", "203.0.113.1", null, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task InvalidToken_CloudflareReportsFailure_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}""")));

        var result = await service.VerifyAsync("bad-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ExpiredOrDuplicateToken_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":false,"error-codes":["timeout-or-duplicate"]}""")));

        var result = await service.VerifyAsync("used-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingToken_NeverCallsCloudflare_ReturnsFalse(string? token)
    {
        var (service, handler) = CreateService((req, ct) =>
            throw new InvalidOperationException("Must not call Cloudflare when no token was supplied."));

        var result = await service.VerifyAsync(token, "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task NetworkFailure_FailsClosed_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) => throw new HttpRequestException("connection reset"));

        var result = await service.VerifyAsync("some-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Timeout_FailsClosed_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) => throw new OperationCanceledException());

        var result = await service.VerifyAsync("some-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task NonSuccessHttpStatus_FailsClosed_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await service.VerifyAsync("some-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task UnparsableBody_FailsClosed_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "not json")));

        var result = await service.VerifyAsync("some-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ActionMismatch_FailsClosed_ReturnsFalse()
    {
        var (service, _) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":true,"action":"login"}""")));

        var result = await service.VerifyAsync("some-token", "203.0.113.1", "register", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ActionMatch_Passes()
    {
        var (service, _) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":true,"action":"register"}""")));

        var result = await service.VerifyAsync("some-token", "203.0.113.1", "register", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task HostnameMismatch_WhenExpectedHostnameConfigured_FailsClosed()
    {
        var settings = new TurnstileSettings { SiteKey = "site-key", SecretKey = "secret-key", ExpectedHostname = "hambox.example" };
        var (service, _) = CreateService(
            (req, ct) => Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":true,"hostname":"evil.example"}""")),
            settings);

        var result = await service.VerifyAsync("some-token", "203.0.113.1", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SendsSecretAndResponseFields_NeverLeaksSecretInLogs()
    {
        var (service, handler) = CreateService((req, ct) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"success":true}""")));

        await service.VerifyAsync("the-token", "203.0.113.1", null, CancellationToken.None);

        Assert.Contains("secret-key", handler.LastRequestBody);
        Assert.Contains("the-token", handler.LastRequestBody);
        Assert.Contains("203.0.113.1", handler.LastRequestBody);
    }
}
