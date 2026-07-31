namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// The result of uploading a category's image.
/// </summary>
/// <param name="ImageUrl">The URL of the newly stored image.</param>
public sealed record CategoryImageDto(string ImageUrl);
