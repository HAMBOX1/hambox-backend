using System.Security.Cryptography;
using System.Text;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.API.Extensions;

/// <summary>
/// An EXTERNAL watchdog target (audit R44). The in-process "worker heartbeat is stale" alert can never fire when the
/// whole process is down or wedged, so something outside the process must poll this. It is anonymous but gated by a
/// shared secret header (<c>Monitoring:WatchdogToken</c>); when no token is configured the endpoint does not exist.
/// Returns 200 when the worker is alive and nothing is stuck, 503 otherwise — what a cron / uptime monitor needs.
/// </summary>
internal static class WorkerHealthEndpoints
{
    public const string TokenHeader = "X-Monitor-Token";
    public static readonly TimeSpan MaxHeartbeatAge = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(10);

    public static IEndpointRouteBuilder MapWorkerHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/system/worker-health", async (
            HttpContext http,
            IConfiguration configuration,
            IWorkerRuntimeState worker,
            ICommerceDbContext db,
            CancellationToken cancellationToken) =>
        {
            var expected = configuration["Monitoring:WatchdogToken"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                return Results.NotFound();
            }

            var supplied = http.Request.Headers[TokenHeader].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var report = await EvaluateAsync(worker, db, DateTimeOffset.UtcNow, cancellationToken);
            return Results.Json(report, statusCode: report.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("GetWorkerHealth")
        .AllowAnonymous();

        return app;
    }

    public sealed record WorkerHealthReport(
        bool Healthy,
        double? HeartbeatAgeSeconds,
        int StuckJobs,
        int DeadLetterJobs,
        IReadOnlyList<string> Problems);

    internal static async Task<WorkerHealthReport> EvaluateAsync(
        IWorkerRuntimeState worker, ICommerceDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        double? age = worker.LastHeartbeatUtc is { } beat ? (now - beat).TotalSeconds : null;
        if (age is null || age > MaxHeartbeatAge.TotalSeconds)
        {
            problems.Add(age is null ? "The worker has never reported a heartbeat." : $"The worker heartbeat is {age:N0}s old.");
        }

        var cutoff = now - StuckAfter;
        var stuck = await db.OperationalJobs.CountAsync(
            j => (j.Status == OperationalJobStatus.Queued && j.CreatedOnUtc <= cutoff)
                || (j.Status == OperationalJobStatus.Running && j.StartedOnUtc != null && j.StartedOnUtc <= cutoff),
            cancellationToken);
        if (stuck > 0)
        {
            problems.Add($"{stuck} job(s) have been queued or running for more than {StuckAfter.TotalMinutes:0} minutes.");
        }

        var dead = await db.OperationalJobs.CountAsync(j => j.Status == OperationalJobStatus.DeadLetter, cancellationToken);

        return new WorkerHealthReport(problems.Count == 0, age, stuck, dead, problems);
    }
}
