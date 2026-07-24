namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents a product's private post-purchase documentation, as seen by an admin editor.
/// </summary>
public sealed record ProductInstructionsDto(
    Guid ProductId,
    string Title,
    string ContentHtml,
    int Version,
    bool IsPublished,
    DateTimeOffset? UpdatedOnUtc);
