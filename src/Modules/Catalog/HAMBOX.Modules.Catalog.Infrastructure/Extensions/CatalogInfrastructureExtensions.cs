using FluentValidation;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Infrastructure.BackgroundJobs;
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.Modules.Catalog.Application.Features.Categories.CreateCategory;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Catalog.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Catalog module infrastructure services.
/// </summary>
public static class CatalogInfrastructureExtensions
{
    /// <summary>
    /// Registers database context and FluentValidation validators for the Catalog module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configure DbContext
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<CatalogDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"))
            .AddInterceptors(
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                new ProductAggregateChangeInterceptor(),
                sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IInventoryEngine, InventoryEngine>();
        services.AddScoped<ProductionDemoDataSeeder>();

        // Catalog import/export
        services.AddSingleton<ICatalogPackageSecretProtector, CatalogPackageSecretProtector>();
        services.AddSingleton<IPackageCryptoService, PackageCryptoService>();
        services.AddScoped<IHamboxPackageWriter, HamboxPackageWriter>();
        services.AddScoped<IHamboxPackageReader, HamboxPackageReader>();
        services.AddScoped<ICatalogImportParser, CatalogImportParser>();
        services.AddSingleton<IImportTemplateGenerator, CatalogImportTemplateGenerator>();
        services.AddScoped<ICatalogSuppliersTransactionService, CatalogSuppliersTransactionService>();
        services.AddScoped<IBackgroundJobHandler, ExportCatalogJobHandler>();
        services.AddScoped<IBackgroundJobHandler, ExecuteCatalogImportJobHandler>();

        // 2. Register FluentValidation Validators
        services.AddValidatorsFromAssembly(typeof(CreateCategoryCommandValidator).Assembly);

        return services;
    }
}
