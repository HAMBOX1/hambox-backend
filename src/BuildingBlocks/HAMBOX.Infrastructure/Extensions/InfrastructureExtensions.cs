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
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "uploads", "dataprotection-keys")));
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

        // 3.5 Turnstile (Cloudflare bot verification) — cross-module (Identity's register/forgot-password/
        // resend-verification validators consume it), so registered here rather than in a module's own
        // Infrastructure. ValidateOnStart mirrors the JwtSettings/EmailSettings/Suppliers-provider fail-fast
        // pattern: an unconfigured or malformed SiteKey/SecretKey fails application startup rather than
        // silently letting every account-action endpoint through unverified.
        services.AddSingleton<IValidateOptions<TurnstileSettings>, TurnstileSettingsValidator>();
        services.AddOptions<TurnstileSettings>()
            .Bind(configuration.GetSection(TurnstileSettings.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<ITurnstileVerificationService, TurnstileVerificationService>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
            var turnstileSettings = sp.GetRequiredService<IOptions<TurnstileSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(turnstileSettings.RequestTimeoutSeconds > 0 ? turnstileSettings.RequestTimeoutSeconds : 10);
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

    /// <summary>
    /// Adds the baseline security response headers middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        return app;
    }

    /// <summary>
    /// Builds the "HamboxCors" policy: only explicitly configured, well-formed absolute-URI origins
    /// are ever allowed. If <paramref name="allowedOrigins"/> is missing, empty, or every entry is
    /// malformed, the policy ends up with zero configured origins — which <see cref="CorsPolicy"/>
    /// treats as "reject every cross-origin request" (<c>AllowAnyOrigin</c> stays false, <c>Origins</c>
    /// stays empty). This must never fall back to <c>AllowAnyOrigin()</c>: that would silently accept
    /// requests from any origin whenever the setting is unconfigured or mistyped — fail closed, not open.
    /// </summary>
    public static void ConfigureHamboxCorsPolicy(CorsPolicyBuilder policy, IReadOnlyList<string> allowedOrigins)
    {
        var validOrigins = allowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out _))
            .ToArray();

        if (validOrigins.Length == 0)
        {
            return;
        }

        policy.WithOrigins(validOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }

    private const string DataProtectionKeysSegment = "dataprotection-keys";

    /// <summary>
    /// True when <paramref name="requestPath"/> falls under the DataProtection keyring's public-facing
    /// path segment (see the <c>PersistKeysToFileSystem</c> call above) — used to 404 it before the
    /// static file middleware gets a chance to serve the keyring over HTTP.
    /// </summary>
    public static bool IsDataProtectionKeysRequest(PathString requestPath, string publicBasePath)
    {
        var prefix = $"{publicBasePath.TrimEnd('/')}/{DataProtectionKeysSegment}";
        return requestPath.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
