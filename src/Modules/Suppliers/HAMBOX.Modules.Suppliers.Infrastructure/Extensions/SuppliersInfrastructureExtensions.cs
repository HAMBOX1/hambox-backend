using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;
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
