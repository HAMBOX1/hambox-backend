using HAMBOX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Content.Infrastructure.Persistence;

public sealed class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();

        var optionsBuilder = new DbContextOptionsBuilder<ContentDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "content"));

        return new ContentDbContext(optionsBuilder.Options);
    }
}
