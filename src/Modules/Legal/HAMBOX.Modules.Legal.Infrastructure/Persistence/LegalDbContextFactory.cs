using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HAMBOX.Modules.Legal.Infrastructure.Persistence;

public sealed class LegalDbContextFactory : IDesignTimeDbContextFactory<LegalDbContext>
{
    public LegalDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "API", "HAMBOX.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<LegalDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("Database"),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform"));

        return new LegalDbContext(optionsBuilder.Options);
    }
}
