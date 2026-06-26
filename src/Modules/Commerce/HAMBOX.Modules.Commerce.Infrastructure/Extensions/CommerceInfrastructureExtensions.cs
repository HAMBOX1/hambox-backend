using FluentValidation;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
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
                sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<ICommerceDbContext>(sp => sp.GetRequiredService<CommerceDbContext>());
        services.AddScoped<ICommerceTransactionService, CommerceTransactionService>();

        services.AddValidatorsFromAssembly(typeof(AddCartItemCommandValidator).Assembly);

        return services;
    }
}
