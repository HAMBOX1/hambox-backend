using System.Threading.RateLimiting;
using FluentValidation;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.UpdateWhatsAppBotConfiguration;
using HAMBOX.Modules.Messaging.Application.RateLimiting;
using HAMBOX.Modules.Messaging.Application.Services;
using HAMBOX.Modules.Messaging.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Infrastructure.Providers;
using HAMBOX.Modules.Messaging.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Messaging.Infrastructure.Extensions;

public static class MessagingInfrastructureExtensions
{
    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<MessagingDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "messaging"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IMessagingDbContext>(sp => sp.GetRequiredService<MessagingDbContext>());

        services.AddScoped<IWhatsAppUserContextScope, HttpContextWhatsAppUserContextScope>();
        services.AddScoped<WhatsAppLinkVerificationService>();
        services.AddScoped<IWhatsAppBotConfigurationProvider, WhatsAppBotConfigurationProvider>();
        services.AddScoped<IWhatsAppBotEngine, WhatsAppBotEngine>();

        services.AddValidatorsFromAssembly(typeof(UpdateWhatsAppBotConfigurationCommandValidator).Assembly);

        // No Meta/WhatsApp credentials exist yet (owner hasn't provided them) — always the fake
        // provider for now. A future MetaWhatsAppProvider plugs in here, chosen when credentials are
        // configured; nothing above this line (the engine, the DbContext, the endpoint) changes.
        services.AddScoped<IWhatsAppProvider, FakeWhatsAppProvider>();

        // Partitioned per caller IP, mirroring Identity's AddFixedWindowPolicy pattern — a bare
        // AddFixedWindowLimiter would be one GLOBAL bucket shared by every caller, so a single fast
        // caller could exhaust the whole budget and lock out every legitimate WhatsApp user.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(MessagingRateLimitPolicies.WhatsAppWebhook, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
