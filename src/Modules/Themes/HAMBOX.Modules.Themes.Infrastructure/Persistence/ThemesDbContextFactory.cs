using HAMBOX.Infrastructure.Persistence;
using HAMBOX.Modules.Themes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HAMBOX.Modules.Themes.Infrastructure.Persistence;

public sealed class ThemesDbContextFactory : IDesignTimeDbContextFactory<ThemesDbContext>
{
    public ThemesDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationFactory.Build();

        var optionsBuilder = new DbContextOptionsBuilder<ThemesDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConfigurationFactory.GetRequiredConnectionString(configuration),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform"));

        return new ThemesDbContext(optionsBuilder.Options);
    }
}
