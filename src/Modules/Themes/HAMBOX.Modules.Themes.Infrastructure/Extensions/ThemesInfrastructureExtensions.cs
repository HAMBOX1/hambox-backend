using FluentValidation;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Features.Campaigns;
using HAMBOX.Modules.Themes.Application.Services;
using HAMBOX.Modules.Themes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Themes.Infrastructure.Extensions;

public static class ThemesInfrastructureExtensions
{
    public static IServiceCollection AddThemesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<ThemesDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IThemesDbContext>(sp => sp.GetRequiredService<ThemesDbContext>());
        services.AddScoped<IThemeEngine, ThemeEngine>();

        // First FluentValidation usage in this module — Campaigns are new, so they follow the
        // broader backend convention (auto-discovered validators) rather than the Themes
        // feature-set's own pre-existing style, which never adopted it.
        services.AddValidatorsFromAssembly(typeof(CreateCampaignCommandValidator).Assembly);

        return services;
    }
}
