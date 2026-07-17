using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Communication;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Infrastructure.BackgroundJobs;
using HAMBOX.Modules.Communication.Infrastructure.Persistence;
using HAMBOX.Modules.Communication.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Communication.Infrastructure.Extensions;

public static class CommunicationInfrastructureExtensions
{
    public static IServiceCollection AddCommunicationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<CommunicationDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "communication"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<ICommunicationDbContext>(sp => sp.GetRequiredService<CommunicationDbContext>());

        services.AddScoped<ICommunicationTemplateRenderer, CommunicationTemplateRenderer>();
        services.AddScoped<ICommunicationPreferenceService, CommunicationPreferenceService>();
        services.AddScoped<ICommunicationService, CommunicationService>();

        // Register every ICommunicationProvider here. Adding a future real channel (Sms, WhatsApp, ...)
        // means adding one more line like this — no other file in the module needs to change.
        services.AddScoped<ICommunicationProvider, DatabaseNotificationProvider>();
        services.AddScoped<ICommunicationProvider, EmailCommunicationProvider>();
        services.AddScoped<ICommunicationProviderRegistry, CommunicationProviderRegistry>();

        services.AddScoped<IBackgroundJobHandler, DeliverCommunicationJobHandler>();

        return services;
    }
}
