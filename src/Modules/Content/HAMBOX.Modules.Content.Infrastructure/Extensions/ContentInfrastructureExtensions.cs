using FluentValidation;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;
using HAMBOX.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Content.Infrastructure.Extensions;

public static class ContentInfrastructureExtensions
{
    public static IServiceCollection AddContentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<ContentDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "content"))
            .AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>()));

        services.AddScoped<IContentDbContext>(sp => sp.GetRequiredService<ContentDbContext>());

        // Legal/Suppliers' validator classes are never wired up this way (an apparent gap in those
        // modules — see PermissionSynchronizer's sibling note); Content follows Catalog/Commerce/Identity's
        // correct convention instead so CreateLandingPageTemplate/SaveLandingPageTemplateDraft validation
        // actually runs through ValidationBehavior.
        services.AddValidatorsFromAssembly(typeof(CreateLandingPageTemplateCommandValidator).Assembly);

        return services;
    }
}
