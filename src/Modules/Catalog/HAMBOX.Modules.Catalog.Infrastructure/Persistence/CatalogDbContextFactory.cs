using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Persistence;
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
        var configuration = DesignTimeConfigurationFactory.Build();
        var connectionString = DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration);

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

        return new CatalogDbContext(optionsBuilder.Options, new DesignTimeCodeProtector());
    }

    // Migration authoring only inspects the model shape; it never calls Protect/Unprotect.
    private sealed class DesignTimeCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;
    }
}
