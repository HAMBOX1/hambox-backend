using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Persistence;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Infrastructure.Services;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Commerce.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for the <see cref="CommerceDbContext"/>.
/// Used by Entity Framework Core CLI tools to generate migrations.
/// </summary>
public sealed class CommerceDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    /// <inheritdoc />
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();
        var connectionString = DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<CommerceDbContext>();
        optionsBuilder.UseSqlServer(connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "commerce"));

        var dateTimeProvider = new SystemDateTimeProvider();
        var currentUserService = new NullCurrentUserService();
        var auditInterceptor = new AuditInterceptor(dateTimeProvider, currentUserService);

        optionsBuilder.AddInterceptors(auditInterceptor);

        return new CommerceDbContext(optionsBuilder.Options, new DesignTimeCodeProtector());
    }

    // Migration authoring only inspects the model shape; it never calls Protect/Unprotect.
    private sealed class DesignTimeCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;
    }
}
