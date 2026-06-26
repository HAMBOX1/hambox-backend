using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Infrastructure.Services;

namespace HAMBOX.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for the <see cref="CatalogDbContext"/>.
/// Used by Entity Framework Core CLI tools to generate migrations.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    /// <inheritdoc />
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        // Navigate to locate the startup API project directory containing appsettings.json
        var root = basePath;
        while (root != null && !Directory.Exists(Path.Combine(root, "src")) && !File.Exists(Path.Combine(root, "HAMBOX.slnx")))
        {
            root = Directory.GetParent(root)?.FullName;
        }

        var apiPath = Path.Combine(root ?? basePath, "src", "API", "HAMBOX.API");
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.Combine(basePath, "..", "..", "API", "HAMBOX.API");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Could not find the database connection string 'Database' in appsettings.json.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseSqlServer(connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"));

        // Use NullCurrentUserService and SystemDateTimeProvider for migrations
        var dateTimeProvider = new SystemDateTimeProvider();
        var currentUserService = new NullCurrentUserService();
        
        var auditInterceptor = new AuditInterceptor(dateTimeProvider, currentUserService);
        var softDeleteInterceptor = new SoftDeleteInterceptor(dateTimeProvider);

        optionsBuilder.AddInterceptors(
            softDeleteInterceptor,
            new Interceptors.ProductAggregateChangeInterceptor(),
            auditInterceptor);

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
