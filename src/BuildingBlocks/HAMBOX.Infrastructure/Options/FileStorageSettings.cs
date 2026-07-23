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
    /// <remarks>
    /// Also covers catalog import/export packages (<c>.hambox</c>/<c>.xlsx</c>/<c>.csv</c>) — this
    /// is the one upload-policy knob the app has (surfaced as Platform Settings' "Media" category),
    /// so it governs any uploaded file type, not only public storefront images.
    /// </remarks>
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "application/zip",
        "application/x-zip-compressed",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/csv"
    ];
}
