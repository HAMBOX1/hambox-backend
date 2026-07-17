using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Register every ISupplierProvider here. Adding a future real integration (e.g. Bamboo) means
        // adding one more line like this — no other file in the module needs to change.
        services.AddScoped<ISupplierProvider, ManualSupplierProvider>();
        services.AddScoped<ISupplierProviderRegistry, SupplierProviderRegistry>();

        return services;
    }
}
