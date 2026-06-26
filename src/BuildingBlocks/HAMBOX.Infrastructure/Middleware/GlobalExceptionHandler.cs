using FluentValidation;
using HAMBOX.Infrastructure.Resources;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Infrastructure.Middleware;

/// <summary>
/// Handles exceptions globally and maps them to RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment,
    IStringLocalizer<SharedResources> localizer) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => CreateValidationProblemDetails(validationException),
            UnauthorizedAccessException => CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                localizer["Errors.Unauthorized.Title"].Value,
                localizer["Errors.Unauthorized.Detail"].Value),
            _ => CreateProblemDetails(
                StatusCodes.Status500InternalServerError,
                localizer["Errors.Internal.Title"].Value,
                environment.IsDevelopment()
                    ? exception.Message
                    : localizer["Errors.Internal.Detail"].Value)
        };

        if (exception is ValidationException)
        {
            logger.LogWarning(exception, "Validation exception: {Message}", exception.Message);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private ProblemDetails CreateValidationProblemDetails(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray());

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = localizer["Errors.Validation.Title"].Value,
            Detail = localizer["Errors.Validation.Detail"].Value,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Extensions = { ["errors"] = errors }
        };
    }

    private static ProblemDetails CreateProblemDetails(
        int statusCode,
        string title,
        string detail)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = statusCode switch
            {
                StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
                StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                _ => "https://tools.ietf.org/html/rfc7231"
            }
        };
    }
}
