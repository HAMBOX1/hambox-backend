using Microsoft.Extensions.Configuration;

namespace HAMBOX.Infrastructure.Persistence;

/// <summary>
/// Builds configuration for EF Core design-time (`dotnet ef`) tooling using the same
/// precedence as the runtime host (<c>WebApplication.CreateBuilder</c>): appsettings.json,
/// then appsettings.{Environment}.json, then user secrets (Development only), then
/// environment variables. Every module's <c>IDesignTimeDbContextFactory</c> should use this
/// instead of hardcoding appsettings.Development.json, so `dotnet ef` behaves consistently
/// across Development, Staging, and Production (e.g. via ConnectionStrings__Database).
/// </summary>
public static class DesignTimeConfigurationFactory
{
    private const string UserSecretsId = "hambox-api-development";

    public static IConfigurationRoot Build()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var builder = new ConfigurationBuilder()
            .SetBasePath(FindApiProjectPath())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddUserSecrets(UserSecretsId);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    public static string GetRequiredConnectionString(IConfiguration configuration, string name = "Database")
    {
        var connectionString = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Could not find the database connection string '{name}'.");
        }

        return connectionString;
    }

    private static string FindApiProjectPath()
    {
        var basePath = Directory.GetCurrentDirectory();

        var root = basePath;
        while (root != null && !Directory.Exists(Path.Combine(root, "src")) && !File.Exists(Path.Combine(root, "HAMBOX.slnx")))
        {
            root = Directory.GetParent(root)?.FullName;
        }

        var apiPath = Path.Combine(root ?? basePath, "src", "API", "HAMBOX.API");
        return Directory.Exists(apiPath) ? apiPath : Path.Combine(basePath, "..", "..", "API", "HAMBOX.API");
    }
}
