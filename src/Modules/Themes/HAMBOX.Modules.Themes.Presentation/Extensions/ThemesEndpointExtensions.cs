using Asp.Versioning;
using HAMBOX.Modules.Themes.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Themes.Presentation.Extensions;

public static class ThemesEndpointExtensions
{
    public static IEndpointRouteBuilder MapThemesEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapThemeEndpoints(apiVersionSet);
        app.MapCampaignEndpoints(apiVersionSet);
        return app;
    }
}
