using HAMBOX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Messaging.Infrastructure.Persistence;

public sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();

        var optionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "messaging"));

        return new MessagingDbContext(optionsBuilder.Options);
    }
}
