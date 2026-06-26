using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Application.Behaviors;

/// <summary>
/// Logs request execution around a request handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling request {RequestName}", requestName);

        long startTimestamp = Stopwatch.GetTimestamp();

        TResponse response = await next(cancellationToken).ConfigureAwait(false);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        logger.LogInformation(
            "Handled request {RequestName} in {ElapsedMs}ms",
            requestName,
            elapsed.TotalMilliseconds);

        return response;
    }
}
