using HAMBOX.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>Reads the current request's token through the same evaluator the endpoint policies use.</summary>
internal sealed class CurrentUserPermissions(IHttpContextAccessor httpContextAccessor) : ICurrentUserPermissions
{
    public bool Has(string permission) =>
        httpContextAccessor.HttpContext?.User is { } user && PermissionClaimEvaluator.HasPermission(user, permission);
}
