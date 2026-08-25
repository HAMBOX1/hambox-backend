using System.Security.Claims;
using HAMBOX.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// Provides information about the current authenticated user using the HTTP context.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    /// <inheritdoc />
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            
            var userId = user?.FindFirst("sub")?.Value 
                         ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                         
            return string.IsNullOrWhiteSpace(userId) ? null : userId;
        }
    }

    /// <inheritdoc />
    public string? DisplayName
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            var email = user?.FindFirst(ClaimTypes.Email)?.Value
                        ?? user?.FindFirst("email")?.Value;

            return string.IsNullOrWhiteSpace(email) ? null : email;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => UserId is not null;

    /// <inheritdoc />
    public bool IsAdminContext =>
        string.Equals(
            httpContextAccessor.HttpContext?.User.FindFirst("auth_context")?.Value,
            "admin",
            StringComparison.OrdinalIgnoreCase);
}
