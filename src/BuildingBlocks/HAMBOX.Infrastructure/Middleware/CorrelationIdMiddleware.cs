using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace HAMBOX.Infrastructure.Middleware;

/// <summary>
/// Middleware that reads or generates a correlation ID for each request,
/// making it available in HttpContext.Items and Serilog LogContext.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// The header name used for correlation IDs.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// Processes the HTTP request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("D");

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
