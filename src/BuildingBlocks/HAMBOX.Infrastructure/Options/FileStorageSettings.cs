namespace HAMBOX.Infrastructure.Options;

/// <summary>
/// Configuration for file storage providers.
/// </summary>
public sealed class FileStorageSettings
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Gets or sets the storage provider name.
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Gets or sets the local root directory for stored files.
    /// </summary>
    public string LocalRootPath { get; set; } = "uploads";

    /// <summary>
    /// Gets or sets the public request path for locally stored files.
    /// </summary>
    public string PublicBasePath { get; set; } = "/uploads";

    /// <summary>
    /// Gets or sets the maximum allowed file size in bytes.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the allowed MIME content types.
    /// </summary>
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];
}
