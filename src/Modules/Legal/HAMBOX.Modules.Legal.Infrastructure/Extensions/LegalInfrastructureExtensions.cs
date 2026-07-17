using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Legal.Infrastructure.Extensions;

public static class LegalInfrastructureExtensions
{
    public static IServiceCollection AddLegalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<LegalDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
            .AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<ILegalDbContext>(sp => sp.GetRequiredService<LegalDbContext>());

        return services;
    }
}
