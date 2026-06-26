using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for database initialization and migrations.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Applies pending database migrations for the specified DbContext in Development environment only.
    /// </summary>
    /// <typeparam name="TContext">The type of the database context.</typeparam>
    /// <param name="app">The web application.</param>
    /// <returns>A task that represents the asynchronous migration operation.</returns>
    public static async Task ApplyMigrationsAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        
        await context.Database.MigrateAsync();
    }
}
