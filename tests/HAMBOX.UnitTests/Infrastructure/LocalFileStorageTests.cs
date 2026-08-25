using System.Text;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Regression coverage for the stored-file extension deriving exclusively from the validated
/// content type, never the client-supplied file name — a client-controlled extension (e.g. a file
/// named "x.html" uploaded under an otherwise-allowed content type) would let the static-file
/// middleware serve the stored file back as executable HTML/SVG/JS, since it picks Content-Type by
/// on-disk extension.
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"hambox-filestorage-tests-{Guid.NewGuid():N}");

    private LocalFileStorage CreateSut(FileStorageSettings? settings = null)
    {
        settings ??= new FileStorageSettings { LocalRootPath = _tempRoot };
        settings.LocalRootPath = _tempRoot;

        // No IPlatformSettingsProvider registered — LocalFileStorage.ResolveMediaSettings() falls
        // back to the raw FileStorageSettings, which is exactly what we want to test against here.
        var services = new ServiceCollection().BuildServiceProvider();

        return new LocalFileStorage(
            Options.Create(settings),
            new FakeHostEnvironment(),
            services.GetRequiredService<IServiceScopeFactory>());
    }

    private static Stream ContentStream(string text = "content") => new MemoryStream(Encoding.UTF8.GetBytes(text));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("x.html", "image/jpeg", ".jpg")]
    [InlineData("x.htm", "image/png", ".png")]
    [InlineData("x.svg", "image/webp", ".webp")]
    [InlineData("x.xhtml", "image/gif", ".gif")]
    [InlineData("x.js", "text/csv", ".csv")]
    public async Task SaveAsync_IgnoresADangerousClientSuppliedExtension_AndStoresTheContentTypeDerivedOneInstead(
        string maliciousFileName, string contentType, string expectedExtension)
    {
        var sut = CreateSut();

        var result = await sut.SaveAsync(ContentStream(), maliciousFileName, contentType, "uploads");

        Assert.EndsWith(expectedExtension, result.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain(".html", result.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".htm", result.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".svg", result.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".xhtml", result.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".js", result.StorageKey, StringComparison.OrdinalIgnoreCase);

        // The original client file name is still preserved for display purposes — it's the on-disk
        // storage extension (what static-file middleware actually serves by) that must never come
        // from client input.
        Assert.Equal(maliciousFileName, result.FileName);
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg", ".jpg")]
    [InlineData("photo.png", "image/png", ".png")]
    [InlineData("photo.webp", "image/webp", ".webp")]
    [InlineData("photo.gif", "image/gif", ".gif")]
    [InlineData("archive.zip", "application/zip", ".zip")]
    [InlineData("archive.zip", "application/x-zip-compressed", ".zip")]
    [InlineData("catalog.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx")]
    [InlineData("data.csv", "text/csv", ".csv")]
    public async Task SaveAsync_EveryCurrentlySupportedContentType_StillStoresWithItsCorrectExtension(
        string fileName, string contentType, string expectedExtension)
    {
        var sut = CreateSut();

        var result = await sut.SaveAsync(ContentStream(), fileName, contentType, "uploads");

        Assert.EndsWith(expectedExtension, result.StorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_ContentTypeWithNoKnownExtensionMapping_FallsBackToTheSafeGenericExtension()
    {
        // Simulates an admin having widened Platform Settings' allowed content types beyond this
        // hard-coded map (e.g. to "text/plain") — the fix must fail closed to a inert extension
        // rather than trusting the client file name for the gap.
        var settings = new FileStorageSettings
        {
            LocalRootPath = _tempRoot,
            AllowedContentTypes = ["text/plain"],
        };
        var sut = CreateSut(settings);

        var result = await sut.SaveAsync(ContentStream(), "notes.html", "text/plain", "uploads");

        Assert.EndsWith(".bin", result.StorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_FileNameWithoutAnyExtension_StillDerivesExtensionFromContentTypeOnly()
    {
        var sut = CreateSut();

        var result = await sut.SaveAsync(ContentStream(), "no-extension-at-all", "image/png", "uploads");

        Assert.EndsWith(".png", result.StorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_DisallowedContentType_IsRejected_RegardlessOfFileName()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SaveAsync(ContentStream(), "innocuous.jpg", "text/html", "uploads"));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "HAMBOX.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
