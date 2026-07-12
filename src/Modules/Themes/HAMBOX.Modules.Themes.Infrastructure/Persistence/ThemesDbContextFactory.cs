using HAMBOX.Modules.Themes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HAMBOX.Modules.Themes.Infrastructure.Persistence;

public sealed class ThemesDbContextFactory : IDesignTimeDbContextFactory<ThemesDbContext>
{
    public ThemesDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "API", "HAMBOX.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ThemesDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("Database"),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform"));

        return new ThemesDbContext(optionsBuilder.Options);
    }
}
