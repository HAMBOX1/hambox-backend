using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HAMBOX.Modules.Communication.Infrastructure.Persistence;

public sealed class CommunicationDbContextFactory : IDesignTimeDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "API", "HAMBOX.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CommunicationDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("Database"),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "communication"));

        return new CommunicationDbContext(optionsBuilder.Options);
    }
}
