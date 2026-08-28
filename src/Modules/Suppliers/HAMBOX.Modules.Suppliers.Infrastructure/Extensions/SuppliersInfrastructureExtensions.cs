using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Extensions;

public static class SuppliersInfrastructureExtensions
{
    public static IServiceCollection AddSuppliersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<SuppliersDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "suppliers"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>(), sp.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<ISuppliersDbContext>(sp => sp.GetRequiredService<SuppliersDbContext>());

        // Register every ISupplierProvider here. Adding a future real integration means adding one more
        // line like this — no other file in the module needs to change.
        services.AddScoped<ISupplierProvider, ManualSupplierProvider>();
        services.AddScoped<ISupplierProvider, BambooSupplierProvider>();
        services.AddScoped<ISupplierProvider, VisoriaSupplierProvider>();
        services.AddScoped<ISupplierProvider, GlobeTopperSupplierProvider>();
        services.AddScoped<ISupplierProvider, EnebaSupplierProvider>();
        services.AddScoped<ISupplierProvider, CodesWholesaleSupplierProvider>();
        services.AddScoped<ISupplierProviderRegistry, SupplierProviderRegistry>();

        // Non-secret HTTP tuning only — Bamboo credentials come from the encrypted Supplier entity per
        // supplier row, never from configuration. Fail-fast at startup via ValidateOnStart, mirroring
        // the JwtSettings/EmailSettings pattern elsewhere in this codebase.
        services.AddSingleton<IValidateOptions<BambooProviderOptions>, BambooProviderOptionsValidator>();
        services.AddOptions<BambooProviderOptions>()
            .Bind(configuration.GetSection(BambooProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<BambooHttpClient>((sp, client) =>
            {
                client.BaseAddress = new Uri(BambooProviderConstants.BaseUrl);
                var bambooOptions = sp.GetRequiredService<IOptions<BambooProviderOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(bambooOptions.RequestTimeoutSeconds);
                client.MaxResponseContentBufferSize = bambooOptions.MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Same non-secret-only shape as Bamboo above — Visoria's API key lives only in the encrypted
        // Supplier.BearerToken column, never in configuration.
        services.AddSingleton<IValidateOptions<VisoriaProviderOptions>, VisoriaProviderOptionsValidator>();
        services.AddOptions<VisoriaProviderOptions>()
            .Bind(configuration.GetSection(VisoriaProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<VisoriaHttpClient>((sp, client) =>
            {
                client.BaseAddress = new Uri(VisoriaProviderConstants.BaseUrl);
                var visoriaOptions = sp.GetRequiredService<IOptions<VisoriaProviderOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(visoriaOptions.RequestTimeoutSeconds);
                client.MaxResponseContentBufferSize = visoriaOptions.MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Same non-secret-only shape as Bamboo/Visoria above — GlobeTopper's key/secret pair lives only
        // in the encrypted Supplier.ApiKey/ApiSecret columns, never in configuration.
        services.AddSingleton<IValidateOptions<GlobeTopperProviderOptions>, GlobeTopperProviderOptionsValidator>();
        services.AddOptions<GlobeTopperProviderOptions>()
            .Bind(configuration.GetSection(GlobeTopperProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<GlobeTopperHttpClient>((sp, client) =>
            {
                client.BaseAddress = new Uri(GlobeTopperProviderConstants.BaseUrl);
                var globeTopperOptions = sp.GetRequiredService<IOptions<GlobeTopperProviderOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(globeTopperOptions.RequestTimeoutSeconds);
                client.MaxResponseContentBufferSize = globeTopperOptions.MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Same non-secret-only shape as Bamboo/Visoria/GlobeTopper above — Eneba's Auth ID/Auth
        // Secret/account email live only in the encrypted Supplier.OAuthSettingsJson column, never in
        // configuration. IMemoryCache (already registered elsewhere in the host) backs the OAuth
        // access-token cache inside EnebaHttpClient.
        services.AddSingleton<IValidateOptions<EnebaProviderOptions>, EnebaProviderOptionsValidator>();
        services.AddOptions<EnebaProviderOptions>()
            .Bind(configuration.GetSection(EnebaProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<EnebaHttpClient>((sp, client) =>
            {
                client.BaseAddress = new Uri(EnebaProviderConstants.BaseUrl);
                var enebaOptions = sp.GetRequiredService<IOptions<EnebaProviderOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(enebaOptions.RequestTimeoutSeconds);
                client.MaxResponseContentBufferSize = Math.Max(enebaOptions.MaxResponseBytes, enebaOptions.MaxArchiveBytes);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Same non-secret-only shape as Bamboo/Visoria/GlobeTopper/Eneba above — CodesWholesale's Client
        // ID/Client Secret live only in the encrypted Supplier.ApiKey/ApiSecret columns, never in
        // configuration. No BaseAddress is set here (unlike every other provider's typed client) —
        // CodesWholesale genuinely has two different hosts (Sandbox/Production), chosen per-Supplier-row
        // by CodesWholesaleHttpClient.ResolveBaseUrl from the non-secret Supplier.SettingsJson
        // "environment" field, so every request builds its own absolute URL. IMemoryCache (already
        // registered elsewhere in the host) backs the OAuth access-token cache, same as Eneba.
        services.AddSingleton<IValidateOptions<CodesWholesaleProviderOptions>, CodesWholesaleProviderOptionsValidator>();
        services.AddOptions<CodesWholesaleProviderOptions>()
            .Bind(configuration.GetSection(CodesWholesaleProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<CodesWholesaleHttpClient>((sp, client) =>
            {
                var cwOptions = sp.GetRequiredService<IOptions<CodesWholesaleProviderOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(cwOptions.RequestTimeoutSeconds);
                client.MaxResponseContentBufferSize = cwOptions.MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Reuses the shared background-job engine (HAMBOX.Application.BackgroundJobs) — the sweep is
        // just another IBackgroundJobHandler the same generic worker dispatches to, no new hosted
        // service. Scheduled from Program.cs via IRecurringJobScheduler.
        services.AddScoped<IBackgroundJobHandler, SupplierFulfillmentSweepJobHandler>();
        services.AddScoped<IBackgroundJobHandler, SupplierAvailabilitySyncJobHandler>();

        services.AddSingleton<IValidateOptions<SupplierAvailabilityOptions>, SupplierAvailabilityOptionsValidator>();
        services.AddOptions<SupplierAvailabilityOptions>()
            .Bind(configuration.GetSection(SupplierAvailabilityOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
