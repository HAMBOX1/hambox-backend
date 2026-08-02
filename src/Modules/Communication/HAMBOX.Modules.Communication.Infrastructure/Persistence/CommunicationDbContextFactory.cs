using HAMBOX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Communication.Infrastructure.Persistence;

public sealed class CommunicationDbContextFactory : IDesignTimeDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();

        var optionsBuilder = new DbContextOptionsBuilder<CommunicationDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "communication"));

        return new CommunicationDbContext(optionsBuilder.Options);
    }
}
