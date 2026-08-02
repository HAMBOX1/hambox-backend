using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Persistence;

public sealed class SuppliersDbContextFactory : IDesignTimeDbContextFactory<SuppliersDbContext>
{
    public SuppliersDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();

        var optionsBuilder = new DbContextOptionsBuilder<SuppliersDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration),
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
