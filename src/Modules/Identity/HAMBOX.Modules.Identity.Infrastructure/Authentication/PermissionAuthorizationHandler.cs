using HAMBOX.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (PermissionClaimEvaluator.HasPermission(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
