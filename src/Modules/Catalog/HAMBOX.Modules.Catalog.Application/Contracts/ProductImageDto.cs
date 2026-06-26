namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents a product image with metadata.
/// </summary>
public sealed record ProductImageDto(
    Guid Id,
    string Url,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    int DisplayOrder,
    bool IsPrimary);
