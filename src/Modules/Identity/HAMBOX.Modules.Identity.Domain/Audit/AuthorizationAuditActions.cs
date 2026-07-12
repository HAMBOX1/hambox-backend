namespace HAMBOX.Modules.Identity.Domain.Audit;

/// <summary>
/// Well-known authorization audit action names.
/// </summary>
public static class AuthorizationAuditActions
{
    public const string RoleCreated = "Role.Created";
    public const string RoleUpdated = "Role.Updated";
    public const string RoleDeleted = "Role.Deleted";
    public const string RoleDuplicated = "Role.Duplicated";
    public const string PermissionsChanged = "Role.PermissionsChanged";
    public const string UserAssigned = "Role.UserAssigned";
    public const string UserRemoved = "Role.UserRemoved";
}
