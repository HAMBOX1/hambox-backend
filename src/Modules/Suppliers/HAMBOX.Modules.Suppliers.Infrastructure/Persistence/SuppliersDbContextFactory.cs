using HAMBOX.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Persistence;

public sealed class SuppliersDbContextFactory : IDesignTimeDbContextFactory<SuppliersDbContext>
{
    public SuppliersDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "API", "HAMBOX.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SuppliersDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("Database"),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "suppliers"));

        return new SuppliersDbContext(optionsBuilder.Options, new DesignTimeCodeProtector());
    }

    // Migration authoring only inspects the model shape; it never calls Protect/Unprotect.
    private sealed class DesignTimeCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;
    }
}
