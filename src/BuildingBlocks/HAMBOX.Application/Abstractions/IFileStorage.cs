namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Represents the result of a stored file operation.
/// </summary>
/// <param name="StorageKey">The provider-specific storage key used for deletion.</param>
/// <param name="PublicUrl">The publicly accessible URL for the stored file.</param>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The MIME content type.</param>
/// <param name="FileSizeBytes">The file size in bytes.</param>
public sealed record StoredFileResult(
    string StorageKey,
    string PublicUrl,
    string FileName,
    string ContentType,
    long FileSizeBytes);

/// <summary>
/// Abstracts file storage for local and cloud providers.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Gets the maximum allowed file size in bytes.
    /// </summary>
    long MaxFileSizeBytes { get; }

    /// <summary>
    /// Saves a file to storage.
    /// </summary>
    Task<StoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored file.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a previously stored file for reading. Every other <see cref="IFileStorage"/> consumer
    /// only writes and later serves the file via <see cref="StoredFileResult.PublicUrl"/> through
    /// static file middleware; catalog import/export is the first caller that needs the bytes back
    /// server-side (to parse an uploaded package, or to stream a generated export for download).
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the content type is allowed.
    /// </summary>
    bool IsAllowedContentType(string contentType);
}
