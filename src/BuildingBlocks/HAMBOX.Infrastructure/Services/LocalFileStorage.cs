using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Options;
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

    public LocalFileStorage(IOptions<FileStorageSettings> options, IHostEnvironment environment)
    {
        _settings = options.Value;
        _rootPath = Path.IsPathRooted(_settings.LocalRootPath)
            ? _settings.LocalRootPath
            : Path.Combine(environment.ContentRootPath, _settings.LocalRootPath);
    }

    /// <inheritdoc />
    public long MaxFileSizeBytes => _settings.MaxFileSizeBytes;

    /// <inheritdoc />
    public bool IsAllowedContentType(string contentType) =>
        _settings.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

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

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".bin"
            };
        }

        var storageKey = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
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
}
