namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents a collection in the unpaged admin tree view, including counts used
/// to render the tree without additional round-trips.
/// </summary>
public sealed record CollectionTreeItemDto(
    Guid Id,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    Guid? ParentId,
    int SortOrder,
    bool IsSystem,
    int ChildrenCount,
    int ProductCount);
