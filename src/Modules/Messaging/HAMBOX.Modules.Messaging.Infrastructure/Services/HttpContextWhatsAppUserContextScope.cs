using System.Security.Claims;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Modules.Messaging.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IWhatsAppUserContextScope"/> the same way every authenticated HTTP request
/// already identifies its caller — by <c>HttpContext.User</c>, which the shared <c>CurrentUserService</c>
/// reads a "sub"/<see cref="ClaimTypes.NameIdentifier"/> claim from. Swaps in a minimal principal for
/// the linked customer for the duration of the scope, then restores whatever was there before
/// (normally the anonymous principal ASP.NET Core sets for an unauthenticated webhook request).
/// </summary>
internal sealed class HttpContextWhatsAppUserContextScope(IHttpContextAccessor httpContextAccessor)
    : IWhatsAppUserContextScope
{
    public IDisposable ActAsCustomer(string customerUserId)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The WhatsApp bot engine must run inside an HTTP request.");

        var previousUser = httpContext.User;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, customerUserId)], authenticationType: "WhatsAppLink"));

        return new RestoreScope(httpContext, previousUser);
    }

    private sealed class RestoreScope(HttpContext httpContext, ClaimsPrincipal previousUser) : IDisposable
    {
        public void Dispose() => httpContext.User = previousUser;
    }
}
