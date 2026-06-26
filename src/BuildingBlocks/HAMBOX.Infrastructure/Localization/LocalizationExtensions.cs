using System.Globalization;
using HAMBOX.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Infrastructure.Localization;

/// <summary>
/// Registers ASP.NET Core localization for HAMBOX (en, ar).
/// </summary>
public static class LocalizationExtensions
{
    public const string CultureCookieName = "hambox-culture";

    public static readonly CultureInfo[] SupportedCultures =
    [
        new("en"),
        new("ar")
    ];

    public static IServiceCollection AddHamboxLocalization(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = SupportedCultures;
            options.SupportedUICultures = SupportedCultures;
            options.ApplyCurrentCultureToResponseHeaders = true;

            options.RequestCultureProviders =
            [
                new AcceptLanguageHeaderRequestCultureProvider(),
                new CookieRequestCultureProvider
                {
                    CookieName = CultureCookieName
                }
            ];
        });

        services.AddScoped<ILocalizedErrorService, LocalizedErrorService>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();

        return services;
    }

    public static WebApplication UseHamboxLocalization(this WebApplication app)
    {
        app.UseRequestLocalization();
        return app;
    }
}
