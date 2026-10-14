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

        // The on-disk extension is what static-file middleware serves (and what a browser executes) by, so it
        // comes from the validated content type only — never from the client-supplied file name. A content
        // type with no known mapping falls back to an inert ".bin" rather than trusting the name.
        var extension = ExtensionForContentType(contentType);

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

    private static string ExtensionForContentType(string contentType) =>
        contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "application/zip" or "application/x-zip-compressed" => ".zip",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "text/csv" => ".csv",
            "application/pdf" => ".pdf",
            _ => ".bin",
        };

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
