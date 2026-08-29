using FluentValidation;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Idempotency;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Commerce module infrastructure services.
/// </summary>
public static class CommerceInfrastructureExtensions
{
    /// <summary>
    /// Registers database context and FluentValidation validators for the Commerce module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddCommerceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<CommerceDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "commerce"))
            .AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<ICommerceDbContext>(sp => sp.GetRequiredService<CommerceDbContext>());
        services.AddScoped<ICommerceTransactionService, CommerceTransactionService>();
        services.AddScoped<DevelopmentPaymentProvider>();
        services.AddScoped<ImmediatePaymentProvider>();
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<DevelopmentPaymentProvider>());
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<ImmediatePaymentProvider>());
        services.AddScoped<PaymentProviderResolver>();
        services.AddScoped<ICheckoutConfigurationProvider, CheckoutConfigurationProvider>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();

        services.Configure<DotSettings>(configuration.GetSection(DotSettings.SectionName));
        services.AddSingleton<IValidateOptions<DotSettings>, DotSettingsValidator>();
        services.AddScoped<IDotPricePointResolver, DotPricePointResolver>();
        services.AddHttpClient<IDotPaymentGateway, DotPaymentGateway>((sp, client) =>
            {
                var dotSettings = sp.GetRequiredService<IOptions<DotSettings>>().Value;
                if (!string.IsNullOrWhiteSpace(dotSettings.BaseUrl))
                {
                    client.BaseAddress = new Uri(dotSettings.BaseUrl);
                }

                client.Timeout = TimeSpan.FromSeconds(dotSettings.RequestTimeoutSeconds > 0 ? dotSettings.RequestTimeoutSeconds : 15);
                client.MaxResponseContentBufferSize = 1024 * 1024;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.Configure<DotFawrySettings>(configuration.GetSection(DotFawrySettings.SectionName));
        services.AddSingleton<IValidateOptions<DotFawrySettings>, DotFawrySettingsValidator>();
        services.AddScoped<IDotFawryChargeAmountResolver, DotFawryChargeAmountResolver>();

        // Cost-based automated-supplier selection — reuses the same CurrencyExchangeRateService
        // DotFawryChargeAmountResolver already relies on (registered by AddSharedInfrastructure).
        services.AddScoped<ISupplierRoutingEngine, SupplierRoutingEngine>();
        services.AddScoped<ISupplierPricingEngine, SupplierPricingEngine>();
        services.AddHttpClient<IDotFawryPaymentGateway, DotFawryPaymentGateway>((sp, client) =>
            {
                var dotFawrySettings = sp.GetRequiredService<IOptions<DotFawrySettings>>().Value;
                client.Timeout = TimeSpan.FromSeconds(dotFawrySettings.RequestTimeoutSeconds > 0 ? dotFawrySettings.RequestTimeoutSeconds : 15);
                client.MaxResponseContentBufferSize = 1024 * 1024;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddValidatorsFromAssembly(typeof(AddCartItemCommandValidator).Assembly);

        services.AddSingleton<IWorkerRuntimeState, WorkerRuntimeState>();

        // Default background-job engine: one storage class serves both the Commerce-internal queue
        // surface and the cross-module IBackgroundJobScheduler surface (see OperationalJobQueue).
        services.AddScoped<OperationalJobQueue>();
        services.AddScoped<IOperationalJobQueue>(sp => sp.GetRequiredService<OperationalJobQueue>());
        services.AddScoped<IBackgroundJobScheduler>(sp => sp.GetRequiredService<OperationalJobQueue>());

        // Every existing operational job type as its own handler — one line each, mirrors
        // SuppliersInfrastructureExtensions' ISupplierProvider registration block.
        services.AddScoped<IBackgroundJobHandler, ExpireInventoryReservationsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, RetryOrderFulfillmentJobHandler>();
        services.AddScoped<IBackgroundJobHandler, ExecuteOrderFulfillmentJobHandler>();
        services.AddScoped<IBackgroundJobHandler, RetryDeliveryJobHandler>();
        services.AddScoped<IBackgroundJobHandler, HealthProbeJobHandler>();
        services.AddScoped<IBackgroundJobHandler, GenerateOperationalAlertsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, SupplierHealthCheckJobHandler>();
        services.AddScoped<IBackgroundJobHandler, BackInStockAlertJobHandler>();
        services.AddScoped<IBackgroundJobHandler, PriceDropAlertJobHandler>();
        services.AddScoped<IBackgroundJobHandler, ReconcileDotPaymentsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, ReconcileDotFawryPaymentsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, RecomputeSupplierDerivedPricingJobHandler>();

        services.AddScoped<IOperationsMonitorService, OperationsMonitorService>();
        services.AddScoped<IAnalyticsAggregationService, AnalyticsAggregationService>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();
        services.AddScoped<IReportCatalog, ReportCatalog>();
        services.AddScoped<IReportBuilderService, ReportBuilderService>();
        services.AddScoped<IReportDocumentGenerator, ReportDocumentGenerator>();
        services.AddScoped<IScheduledReportService, ScheduledReportService>();
        services.AddHostedService<OperationalJobWorker>();
        services.AddHostedService<ScheduledReportWorker>();

        return services;
    }
}
