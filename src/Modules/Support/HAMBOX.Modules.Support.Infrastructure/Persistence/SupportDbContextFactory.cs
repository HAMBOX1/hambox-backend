using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HAMBOX.Modules.Support.Infrastructure.Persistence;

public sealed class SupportDbContextFactory : IDesignTimeDbContextFactory<SupportDbContext>
{
    public SupportDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "API", "HAMBOX.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SupportDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("Database"),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "support"));

        return new SupportDbContext(optionsBuilder.Options);
    }
}
