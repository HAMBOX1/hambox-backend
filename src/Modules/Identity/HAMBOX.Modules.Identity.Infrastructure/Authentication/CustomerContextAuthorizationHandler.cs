using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

internal sealed class CustomerContextAuthorizationHandler
    : AuthorizationHandler<CustomerContextRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CustomerContextRequirement requirement)
    {
        var authContext = context.User.FindFirst(IdentityClaimTypes.AuthContext)?.Value;

        if (string.IsNullOrEmpty(authContext) || authContext == AuthContextTypes.Customer)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
