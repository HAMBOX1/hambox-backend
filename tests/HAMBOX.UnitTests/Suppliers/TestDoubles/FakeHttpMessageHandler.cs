namespace HAMBOX.UnitTests.Suppliers.TestDoubles;

/// <summary>Deterministic HTTP handler for testing typed HttpClient wrappers (e.g. BambooHttpClient) without any real network call.</summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return await responder(request, cancellationToken);
    }
}
