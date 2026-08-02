using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using HAMBOX.Infrastructure.Persistence;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Infrastructure.Services;

namespace HAMBOX.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for the <see cref="IdentityDbContext"/>.
/// Used by Entity Framework Core CLI tools to generate migrations.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    /// <inheritdoc />
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();
        var connectionString = DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseSqlServer(connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity"));

        // Use NullCurrentUserService and SystemDateTimeProvider for migrations
        var dateTimeProvider = new SystemDateTimeProvider();
        var currentUserService = new NullCurrentUserService();
        
        var auditInterceptor = new AuditInterceptor(dateTimeProvider, currentUserService);
        var softDeleteInterceptor = new SoftDeleteInterceptor(dateTimeProvider);

        optionsBuilder.AddInterceptors(softDeleteInterceptor, auditInterceptor);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
