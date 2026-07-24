namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents an internal, owner-only product collection.
/// </summary>
public sealed record CollectionDto(
    Guid Id,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    Guid? ParentId,
    int SortOrder,
    bool IsSystem);
