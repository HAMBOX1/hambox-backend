using System.Diagnostics;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Operations;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class SystemHealthService(
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    IInventoryEngine inventoryEngine,
    IPlatformSettingsProvider platformSettings,
    IWorkerRuntimeState workerState,
    IMemoryCache memoryCache,
    IOptions<FileStorageSettings> fileStorageOptions,
    IHostEnvironment environment) : ISystemHealthService
{
    public async Task<SystemHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<SystemHealthComponentDto>
        {
            await CheckAsync("Database", async () =>
            {
                _ = await commerceDb.OperationalJobs.AsNoTracking().CountAsync(cancellationToken);
            }, cancellationToken),
            await CheckAsync("API", () => Task.CompletedTask, cancellationToken),
            await CheckAsync("Cache", () =>
            {
                const string key = "__ops_health_probe__";
                memoryCache.Set(key, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
                if (!memoryCache.TryGetValue(key, out _))
                {
                    throw new InvalidOperationException("Memory cache probe failed.");
                }

                return Task.CompletedTask;
            }, cancellationToken),
            await CheckSmtpAsync(cancellationToken),
            await CheckStorageAsync(cancellationToken),
            CheckWorkers(),
            await CheckAsync("Inventory Engine", async () =>
            {
                _ = await inventoryEngine.GetStatisticsAsync(cancellationToken: cancellationToken);
            }, cancellationToken),
            await CheckAsync("Promotion Engine", async () =>
            {
                _ = await commerceDb.Promotions.AsNoTracking()
                    .CountAsync(p => p.Status == PromotionStatus.Active, cancellationToken);
            }, cancellationToken),
            await CheckAsync("Membership Engine", async () =>
            {
                _ = await commerceDb.MembershipPlans.AsNoTracking().CountAsync(cancellationToken);
                _ = await commerceDb.MembershipSubscriptions.AsNoTracking()
                    .CountAsync(s => s.Status == MembershipSubscriptionStatus.Active, cancellationToken);
            }, cancellationToken),
            await CheckAsync("Localization", async () =>
            {
                _ = await platformSettings.GetAsync<LocalizationSettingsPayload>(
                    PlatformSettingsCategoryKeys.Localization,
                    cancellationToken);
            }, cancellationToken),
            await CheckAsync("Settings Cache", async () =>
            {
                _ = await platformSettings.GetGeneralAsync(cancellationToken);
            }, cancellationToken),
            await CheckAsync("Catalog", async () =>
            {
                _ = await catalogDb.Products.AsNoTracking().CountAsync(cancellationToken);
            }, cancellationToken),
        };

        var overall = components.Any(c => c.Status == "Unhealthy")
            ? "Unhealthy"
            : components.Any(c => c.Status == "Degraded")
                ? "Degraded"
                : "Healthy";

        return new SystemHealthDto(overall, DateTimeOffset.UtcNow, components);
    }

    private async Task<SystemHealthComponentDto> CheckSmtpAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var email = await platformSettings.GetEmailAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(email.SmtpHost))
            {
                return new SystemHealthComponentDto("SMTP", "Degraded", "SMTP host not configured.", sw.ElapsedMilliseconds);
            }

            return new SystemHealthComponentDto(
                "SMTP",
                "Healthy",
                $"{email.SmtpHost}:{email.SmtpPort}",
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new SystemHealthComponentDto("SMTP", "Unhealthy", ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private Task<SystemHealthComponentDto> CheckStorageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        try
        {
            var settings = fileStorageOptions.Value;
            var root = Path.IsPathRooted(settings.LocalRootPath)
                ? settings.LocalRootPath
                : Path.Combine(environment.ContentRootPath, settings.LocalRootPath);

            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            var probe = Path.Combine(root, $".ops-health-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            return Task.FromResult(new SystemHealthComponentDto(
                "Storage",
                "Healthy",
                root,
                sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SystemHealthComponentDto(
                "Storage",
                "Unhealthy",
                ex.Message,
                sw.ElapsedMilliseconds));
        }
    }

    private SystemHealthComponentDto CheckWorkers()
    {
        if (!workerState.LastHeartbeatUtc.HasValue)
        {
            return new SystemHealthComponentDto(
                "Background Workers",
                workerState.IsRunning ? "Degraded" : "Unhealthy",
                "No heartbeat yet.",
                null);
        }

        var age = (DateTimeOffset.UtcNow - workerState.LastHeartbeatUtc.Value).TotalSeconds;
        if (age > 120)
        {
            return new SystemHealthComponentDto(
                "Background Workers",
                "Unhealthy",
                $"Heartbeat stale ({(int)age}s).",
                null);
        }

        if (age > 60)
        {
            return new SystemHealthComponentDto(
                "Background Workers",
                "Degraded",
                $"Heartbeat age {(int)age}s.",
                null);
        }

        return new SystemHealthComponentDto(
            "Background Workers",
            "Healthy",
            $"Heartbeat age {(int)age}s. Processed={workerState.ProcessedCount}.",
            null);
    }

    private static async Task<SystemHealthComponentDto> CheckAsync(
        string name,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
            return new SystemHealthComponentDto(name, "Healthy", null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new SystemHealthComponentDto(name, "Unhealthy", ex.Message, sw.ElapsedMilliseconds);
        }
    }
}
