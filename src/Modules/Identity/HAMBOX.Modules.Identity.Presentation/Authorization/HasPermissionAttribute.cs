using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Presentation.Authorization;

/// <summary>
/// Authorizes access when the user has the specified permission policy.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = permission;
    }
}
