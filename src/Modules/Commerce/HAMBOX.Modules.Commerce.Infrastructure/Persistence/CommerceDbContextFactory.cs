using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Infrastructure.Services;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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
        var basePath = Directory.GetCurrentDirectory();

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
