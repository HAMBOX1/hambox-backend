namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents a category in the catalog.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="NameAr">The category name in Arabic.</param>
/// <param name="NameEn">The category name in English.</param>
/// <param name="Slug">The URL-friendly slug.</param>
/// <param name="IsActive">Whether the category is active.</param>
public sealed record CategoryDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string Slug,
    bool IsActive,
    Guid? ParentId);
