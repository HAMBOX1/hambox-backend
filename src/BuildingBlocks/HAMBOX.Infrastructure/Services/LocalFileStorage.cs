using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// Stores files on the local file system.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    /// <summary>
    /// The only source of truth for a stored file's on-disk extension. Deliberately does not fall
    /// back to the client-supplied file name for anything: the static-file middleware
    /// (<c>UseStaticFiles</c> in <c>Program.cs</c>) serves a file's <c>Content-Type</c> by its
    /// on-disk extension, so a client-controlled extension (e.g. a file named <c>x.html</c>
    /// uploaded with an otherwise-allowed content type) would let a stored file be served back as
    /// executable HTML/SVG/JS — stored XSS against whoever opens it. A content type with no entry
    /// here (including anything not covered by this map) falls through to <c>.bin</c>, which the
    /// default <see cref="Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider"/> maps
    /// to <c>application/octet-stream</c> — never renderable as a page by a browser.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ContentTypeExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
            ["application/zip"] = ".zip",
            ["application/x-zip-compressed"] = ".zip",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
            ["application/vnd.ms-excel.sheet.macroEnabled.12"] = ".xlsm",
            ["text/csv"] = ".csv",
        };

    private readonly FileStorageSettings _settings;
    private readonly string _rootPath;
    private readonly IServiceScopeFactory _scopeFactory;

    public LocalFileStorage(
        IOptions<FileStorageSettings> options,
        IHostEnvironment environment,
        IServiceScopeFactory scopeFactory)
    {
        _settings = options.Value;
        _scopeFactory = scopeFactory;
        _rootPath = Path.IsPathRooted(_settings.LocalRootPath)
            ? _settings.LocalRootPath
            : Path.Combine(environment.ContentRootPath, _settings.LocalRootPath);
    }

    /// <inheritdoc />
    public long MaxFileSizeBytes => ResolveMediaSettings().MaxUploadSizeBytes;

    /// <inheritdoc />
    public bool IsAllowedContentType(string contentType) =>
        ResolveMediaSettings().AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<StoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        if (!IsAllowedContentType(contentType))
        {
            throw new InvalidOperationException($"Content type '{contentType}' is not allowed.");
        }

        if (content.CanSeek && content.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"File exceeds the maximum size of {MaxFileSizeBytes} bytes.");
        }

        var extension = ContentTypeExtensions.GetValueOrDefault(contentType, ".bin");

        var storageKey = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(absolutePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await content.CopyToAsync(fileStream, cancellationToken);
        var fileSize = fileStream.Length;

        if (fileSize > MaxFileSizeBytes)
        {
            fileStream.Close();
            File.Delete(absolutePath);
            throw new InvalidOperationException($"File exceeds the maximum size of {MaxFileSizeBytes} bytes.");
        }

        var publicUrl = $"{_settings.PublicBasePath.TrimEnd('/')}/{storageKey.Replace('\\', '/')}";

        return new StoredFileResult(storageKey, publicUrl, fileName, contentType, fileSize);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var absolutePath = Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var absolutePath = Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Stored file '{storageKey}' was not found.", absolutePath);
        }

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private HAMBOX.Application.PlatformSettings.MediaSettingsPayload ResolveMediaSettings()
    {
        using var scope = _scopeFactory.CreateScope();
        var platformSettings = scope.ServiceProvider.GetService<IPlatformSettingsProvider>();
        if (platformSettings is null)
        {
            return new HAMBOX.Application.PlatformSettings.MediaSettingsPayload(
                _settings.MaxFileSizeBytes,
                _settings.AllowedContentTypes,
                true,
                128,
                256,
                512);
        }

        return platformSettings.GetMediaAsync().GetAwaiter().GetResult();
    }
}
