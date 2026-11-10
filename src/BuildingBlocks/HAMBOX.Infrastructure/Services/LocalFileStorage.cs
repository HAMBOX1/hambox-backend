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

        if (!await LooksLikeDeclaredImageAsync(content, contentType, cancellationToken))
        {
            throw new InvalidOperationException($"File content does not match content type '{contentType}'.");
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

        // The stored name is random; the client's name is only display metadata, so keep just a safe base name
        // (no directory parts such as "../../x") and cap its length to the column size.
        var displayName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "upload";
        }

        if (displayName.Length > 200)
        {
            displayName = displayName[^200..];
        }

        return new StoredFileResult(storageKey, publicUrl, displayName, contentType, fileSize);
    }

    /// <summary>
    /// The content type is client-supplied, so for binary types with a fixed signature (images, ZIP / XLSX, PDF)
    /// the leading bytes must carry it. Text types and non-seekable streams are left to the allow-list.
    /// </summary>
    private static async Task<bool> LooksLikeDeclaredImageAsync(
        Stream content, string contentType, CancellationToken cancellationToken)
    {
        var type = contentType.Trim().ToLowerInvariant();
        if (!content.CanSeek)
        {
            return true;
        }

        var header = new byte[12];
        var start = content.Position;
        var read = await content.ReadAsync(header.AsMemory(), cancellationToken);
        content.Position = start;

        return type switch
        {
            "image/png" => read >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            "image/jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/gif" => read >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,
            "image/webp" => read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            "application/zip" or "application/x-zip-compressed"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] is 0x03 or 0x05 or 0x07,
            "application/pdf" => read >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
            _ => true,
        };
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
