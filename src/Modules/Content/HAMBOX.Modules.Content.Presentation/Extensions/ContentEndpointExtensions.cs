using Asp.Versioning;
using HAMBOX.Modules.Content.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Content.Presentation.Extensions;

public static class ContentEndpointExtensions
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapLandingPageTemplateEndpoints(apiVersionSet);
        return app;
    }
}
