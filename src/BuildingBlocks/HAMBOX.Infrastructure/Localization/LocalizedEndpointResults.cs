using HAMBOX.Infrastructure.Localization;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Infrastructure.Localization;

/// <summary>
/// Helpers for returning localized API results from minimal endpoints.
/// </summary>
public static class LocalizedEndpointResults
{
    /// <summary>
    /// Returns OK or a localized error payload.
    /// </summary>
    public static IResult FromResult(HttpContext httpContext, Result result)
    {
        return result.IsSuccess
            ? Results.Ok()
            : Results.BadRequest(CreateErrorPayload(httpContext, result.Error));
    }

    /// <summary>
    /// Returns OK with value or a localized error payload.
    /// </summary>
    public static IResult FromResult<T>(HttpContext httpContext, Result<T> result)
    {
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateErrorPayload(httpContext, result.Error));
    }

    private static object CreateErrorPayload(HttpContext httpContext, Error error)
    {
        var localizer = httpContext.RequestServices.GetRequiredService<ILocalizedErrorService>();
        return new
        {
            Error = new Error(error.Code, localizer.Localize(error))
        };
    }
}
