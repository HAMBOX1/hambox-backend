using System.Diagnostics;
using System.Security.Claims;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Middleware;

public sealed class ApiRequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<ApiRequestLoggingMiddleware> logger)
{
    private static readonly HashSet<string> SkippedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/swagger",
        "/favicon",
        "/uploads",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context.Request.Path))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        Exception? caught = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            caught = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            try
            {
                await PersistAsync(context, sw.ElapsedMilliseconds, caught);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist API request log for {Path}", context.Request.Path);
            }
        }
    }

    private static bool ShouldSkip(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (Path.HasExtension(value) && !value.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SkippedPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An unhandled exception is turned into its error response by the handler further out, after this
    /// middleware has already unwound — so the response still reads 200 here. Log the status the client gets.
    /// </summary>
    private static int ResolveStatusCode(HttpContext context, Exception? exception) => exception switch
    {
        null => context.Response.StatusCode,
        BadHttpRequestException badRequest => badRequest.StatusCode,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static async Task PersistAsync(HttpContext context, long durationMs, Exception? exception)
    {
        using var scope = context.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICommerceDbContext>();

        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User?.FindFirstValue("sub");

        var correlationId = context.TraceIdentifier;
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            correlationId = header.ToString();
        }

        long? requestSize = context.Request.ContentLength;
        long? responseSize = context.Response.ContentLength;

        var log = ApiRequestLog.Create(
            context.Request.Method,
            context.Request.Path + context.Request.QueryString,
            ResolveStatusCode(context, exception),
            durationMs,
            correlationId,
            userId,
            context.Connection.RemoteIpAddress?.ToString(),
            requestSize,
            responseSize,
            exception?.ToString());

        db.ApiRequestLogs.Add(log);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
