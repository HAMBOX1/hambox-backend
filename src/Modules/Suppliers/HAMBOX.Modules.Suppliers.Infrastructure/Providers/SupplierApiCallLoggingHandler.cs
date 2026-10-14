using System.Diagnostics;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers;

/// <summary>
/// Records every outbound supplier API call (audit R40): URL (secrets masked), redacted request body, status,
/// duration, outcome and — via <see cref="SupplierCallScope"/> — which fulfillment attempt it belonged to. The body
/// of a successful response is deliberately not stored (it can contain delivered codes); only its identifying fields.
/// Logging can never affect the call itself: every failure here is swallowed.
/// </summary>
internal sealed class SupplierApiCallLoggingHandler(
    string providerKey,
    IServiceScopeFactory scopeFactory,
    ILogger<SupplierApiCallLoggingHandler> logger) : DelegatingHandler
{
    private const long MaxBodyBytesToRead = 16 * 1024 * 1024;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? requestBody = null;
        if (request.Content is not null)
        {
            try
            {
                requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception)
            {
                // A streaming body that cannot be re-read is simply not logged.
            }
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await WriteAsync(request, requestBody, null, null, ex.GetType().Name + ": " + ex.Message, stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();

        string? responseText = null;
        try
        {
            if (response.Content is not null && (response.Content.Headers.ContentLength ?? 0) <= MaxBodyBytesToRead)
            {
                // Read the body once and hand the caller an identical, replayable copy — the typed client still
                // sees exactly the same status, headers and content (its own buffer-size limit applies as before).
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var replay = new ByteArrayContent(bytes);
                foreach (var header in response.Content.Headers)
                {
                    replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Content.Dispose();
                response.Content = replay;
                responseText = System.Text.Encoding.UTF8.GetString(bytes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unreadable body: the call is still logged (status, duration), just without it.
            logger.LogDebug(ex, "Could not capture the {Provider} response body for the call log.", providerKey);
        }

        await WriteAsync(request, requestBody, response, responseText, null, stopwatch.ElapsedMilliseconds);
        return response;
    }

    private async Task WriteAsync(
        HttpRequestMessage request,
        string? requestBody,
        HttpResponseMessage? response,
        string? responseText,
        string? error,
        long elapsedMs)
    {
        try
        {
            var succeeded = response is { IsSuccessStatusCode: true };
            var info = SupplierCallScope.Current;

            var entry = new SupplierApiCallLog(
                Guid.NewGuid(),
                providerKey,
                request.Method.Method,
                SupplierApiLogSanitizer.SanitizeUrl(request.RequestUri, "(unknown)"),
                SupplierApiLogSanitizer.SanitizeBody(requestBody),
                response is null ? null : (int)response.StatusCode,
                succeeded ? SupplierApiLogSanitizer.SuccessBodyOmitted : SupplierApiLogSanitizer.SanitizeBody(responseText),
                succeeded ? SupplierApiLogSanitizer.Summarise(responseText) : null,
                succeeded,
                error,
                (int)Math.Min(elapsedMs, int.MaxValue),
                info?.FulfillmentId,
                info?.HamboxReferenceId,
                info?.SupplierId,
                info?.OrderId,
                info?.Attempt);

            // A fresh scope = its own DbContext: the log is written independently of whatever unit of work the
            // fulfillment is in the middle of, and never enlists in (or is rolled back with) it.
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SuppliersDbContext>();
            db.SupplierApiCallLogs.Add(entry);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record the {Provider} API call log entry.", providerKey);
        }
    }
}
