using FluentValidation;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Infrastructure.Persistence;
using HAMBOX.Modules.Support.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Support.Infrastructure.Extensions;

public static class SupportInfrastructureExtensions
{
    public static IServiceCollection AddSupportInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<SupportDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "support"))
            .AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<ISupportDbContext>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddScoped<ISupportAiAssistant, NullSupportAiAssistant>();
        services.AddScoped<IAttachmentScanner, NullAttachmentScanner>();
        services.AddScoped<TicketContextBuilder>();

        services.AddValidatorsFromAssembly(typeof(CreateTicketCommandValidator).Assembly);

        return services;
    }
}
