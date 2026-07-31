namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Summary information for a role in list views.
/// </summary>
public sealed record RoleListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsDefault,
    int PriorityLevel,
    int UserCount,
    int PermissionCount,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

/// <summary>
/// Detailed role information including assigned permissions.
/// </summary>
public sealed record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsDefault,
    int PriorityLevel,
    int UserCount,
    IReadOnlyCollection<Guid> PermissionIds,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

/// <summary>
/// A permission group with its child permissions for matrix display.
/// </summary>
public sealed record PermissionGroupDto(
    Guid Id,
    string Key,
    string Name,
    string Module,
    int SortOrder,
    string? Description,
    IReadOnlyCollection<PermissionDto> Permissions);

/// <summary>
/// A single permission entry.
/// </summary>
public sealed record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder);

/// <summary>
/// Request body for creating a custom role.
/// </summary>
public sealed record CreateRoleRequest(string Name, string? Description, int? PriorityLevel);

/// <summary>
/// Request body for updating a role.
/// </summary>
public sealed record UpdateRoleRequest(string Name, string? Description, int? PriorityLevel, bool? IsDefault);

/// <summary>
/// Request body for replacing a role's permission set.
/// </summary>
public sealed record SetRolePermissionsRequest(IReadOnlyCollection<Guid> PermissionIds);

/// <summary>
/// Request body for assigning users to a role.
/// </summary>
public sealed record AssignUsersRequest(IReadOnlyCollection<Guid> UserIds);

/// <summary>
/// User summary for role assignment and search dialogs.
/// </summary>
public sealed record UserSearchResultDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status);
