using System.IO.Compression;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Security;
using HAMBOX.Infrastructure.Currency;
using HAMBOX.Infrastructure.Localization;
using HAMBOX.Infrastructure.Middleware;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Infrastructure.Security;
using HAMBOX.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering shared cross-cutting infrastructure services.
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Name of the subfolder (under the uploads root) that persists the DataProtection keyring —
    /// the key that encrypts every inventory code and license key at rest. Must never be reachable
    /// through the public `/uploads` static-file route; <c>Program.cs</c> blocks this exact segment
    /// before <c>UseStaticFiles</c> runs. Kept as a shared constant so the two call sites can't drift.
    /// </summary>
    public const string DataProtectionKeysFolderName = "dataprotection-keys";

    /// <summary>
    /// True if <paramref name="requestPath"/> falls under the DataProtection keyring's public URL
    /// segment (<paramref name="publicBasePath"/> + <see cref="DataProtectionKeysFolderName"/>) and
    /// must therefore be refused before the static file middleware would otherwise serve it.
    /// </summary>
    public static bool IsDataProtectionKeysRequest(PathString requestPath, string publicBasePath)
    {
        var blockedPath = publicBasePath.TrimEnd('/') + "/" + DataProtectionKeysFolderName;
        return requestPath.StartsWithSegments(blockedPath, StringComparison.OrdinalIgnoreCase);
    }

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

        // Keys must survive app restarts/redeploys or every restart silently invalidates every
        // previously-protected value at rest (inventory codes, license keys). Stored under "uploads"
        // because that directory is explicitly excluded from the WebDeploy delete-sync (see the
        // HamboxWebDeploy publish profile's MsDeploySkipRules), so it isn't wiped on publish.
        services.AddDataProtection()
            .SetApplicationName("Hambox")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "uploads", DataProtectionKeysFolderName)));
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
            options.AddPolicy("HamboxCors", policy => ConfigureHamboxCorsPolicy(policy, allowedOrigins));
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

        // 5b. Cloudflare Turnstile — SecretKey is fail-fast validated at startup (ValidateOnStart),
        // mirroring the JwtSettings/BambooProviderOptions pattern. Registered here (not in a single
        // module) so any module's command validators can require verification without a dependency on
        // a specific module.
        services.AddSingleton<IValidateOptions<TurnstileSettings>, TurnstileSettingsValidator>();
        services.AddOptions<TurnstileSettings>()
            .Bind(configuration.GetSection(TurnstileSettings.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<ITurnstileVerificationService, TurnstileVerificationService>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
            var turnstileOptions = sp.GetRequiredService<IOptions<TurnstileSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(turnstileOptions.RequestTimeoutSeconds);
        });

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

    /// <summary>
    /// Builds the "HamboxCors" policy from configured origins. Fail closed: an empty/missing
    /// <c>Cors:AllowedOrigins</c> must permit no cross-origin browser requests — never
    /// <c>AllowAnyOrigin()</c>. <c>WithOrigins([])</c> accepts no origin, so this only affects
    /// browser CORS preflight/requests; server-to-server and same-origin calls are unaffected.
    /// Extracted as its own method (rather than an inline lambda) so it's directly unit-testable
    /// without booting the app.
    /// </summary>
    public static void ConfigureHamboxCorsPolicy(CorsPolicyBuilder policy, string[] allowedOrigins)
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }

    /// <summary>
    /// Adds the baseline security response headers (X-Content-Type-Options, X-Frame-Options,
    /// Referrer-Policy) to the application pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        return app;
    }
}
