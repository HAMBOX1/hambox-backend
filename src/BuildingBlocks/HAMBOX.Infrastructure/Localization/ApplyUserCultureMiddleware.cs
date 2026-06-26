using System.Globalization;
using HAMBOX.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Infrastructure.Localization;

/// <summary>
/// Applies authenticated user language preference after JWT authentication.
/// </summary>
public sealed class ApplyUserCultureMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IUserLanguagePreferenceResolver resolver)
    {
        var preferredLanguage = await resolver.GetPreferredLanguageAsync(context.RequestAborted);
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            var cultureCode = preferredLanguage.Split('-')[0].ToLowerInvariant();
            if (cultureCode is "en" or "ar")
            {
                var culture = new CultureInfo(cultureCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                context.Response.Headers.ContentLanguage = cultureCode;
            }
        }

        await next(context);
    }
}

/// <summary>
/// Extension methods for user culture middleware.
/// </summary>
public static class ApplyUserCultureMiddlewareExtensions
{
    /// <summary>
    /// Applies saved user culture after authentication when available.
    /// </summary>
    public static IApplicationBuilder UseApplyUserCulture(this IApplicationBuilder app) =>
        app.UseMiddleware<ApplyUserCultureMiddleware>();
}
