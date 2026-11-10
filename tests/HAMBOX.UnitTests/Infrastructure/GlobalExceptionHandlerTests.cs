using HAMBOX.Infrastructure.Middleware;
using HAMBOX.Infrastructure.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Infrastructure;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task MissingRequiredParameter_IsReportedAs400_NotAsAServerError()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment(),
            new KeyLocalizer());

        var handled = await handler.TryHandleAsync(
            context,
            new BadHttpRequestException("Required parameter \"int pageNumber\" was not provided from query string."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedException_StillReturns500()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment(),
            new KeyLocalizer());

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    /// <summary>Echoes the resource key, so the test doesn't depend on the localisation files.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
