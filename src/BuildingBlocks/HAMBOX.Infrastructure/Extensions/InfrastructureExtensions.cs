using System.IO.Compression;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Infrastructure.Currency;
using HAMBOX.Infrastructure.Localization;
using HAMBOX.Infrastructure.Middleware;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering shared cross-cutting infrastructure services.
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Registers shared infrastructure services including exception handling,
    /// response compression, CORS, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 0. HTTP Context, Date Time and Auditing Services
        services.AddHttpContextAccessor();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IIdempotencyKeyAccessor, IdempotencyKeyAccessor>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();

        services.Configure<IdempotencyOptions>(configuration.GetSection(IdempotencyOptions.SectionName));

        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddDataProtection();
        services.AddSingleton<ICodeProtector, DataProtectionCodeProtector>();

        // 1. Exception Handling + ProblemDetails
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // 2. Response Compression
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);

        services.Configure<GzipCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.SmallestSize);

        // 3. CORS
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy("HamboxCors", policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });

        // 4. Health Checks
        var connectionString = configuration.GetConnectionString("Database");
        var healthChecksBuilder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            healthChecksBuilder.AddSqlServer(
                connectionString,
                name: "sqlserver",
                tags: ["db", "sql", "ready"]);
        }

        // 5. Localization + currency
        services.AddHamboxLocalization();
        services.AddHamboxCurrency(configuration);

        // 6. Background jobs — engine-agnostic abstractions consumed by every module.
        // The concrete queue/worker (the swappable "engine") is registered by Commerce.Infrastructure.
        services.AddSingleton<IBackgroundJobSerializer, JsonBackgroundJobSerializer>();
        services.AddScoped<IBackgroundJobHandlerRegistry, BackgroundJobHandlerRegistry>();
        services.AddSingleton<RecurringJobRegistry>();
        services.AddSingleton<IRecurringJobScheduler>(sp => sp.GetRequiredService<RecurringJobRegistry>());
        services.AddSingleton<IRecurringJobRegistry>(sp => sp.GetRequiredService<RecurringJobRegistry>());

        return services;
    }

    /// <summary>
    /// Adds the correlation ID middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}
