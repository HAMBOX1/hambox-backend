using HAMBOX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Legal.Infrastructure.Persistence;

public sealed class LegalDbContextFactory : IDesignTimeDbContextFactory<LegalDbContext>
{
    public LegalDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();

        var optionsBuilder = new DbContextOptionsBuilder<LegalDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform"));

        return new LegalDbContext(optionsBuilder.Options);
    }
}
