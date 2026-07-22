namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents a category in the unpaged admin tree view, including counts used
/// to render the tree without additional round-trips.
/// </summary>
public sealed record CategoryTreeItemDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string Slug,
    bool IsActive,
    Guid? ParentId,
    int SortOrder,
    int ChildrenCount,
    int ProductCount);
