using FluentValidation;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Idempotency;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IBackgroundJobHandler, RetryDeliveryJobHandler>();
        services.AddScoped<IBackgroundJobHandler, HealthProbeJobHandler>();
        services.AddScoped<IBackgroundJobHandler, GenerateOperationalAlertsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, SupplierHealthCheckJobHandler>();

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
