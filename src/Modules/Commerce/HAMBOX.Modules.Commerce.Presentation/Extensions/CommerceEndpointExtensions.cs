using Asp.Versioning;
using HAMBOX.Modules.Commerce.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Extensions;

/// <summary>
/// Extension methods for commerce endpoints.
/// </summary>
public static class CommerceEndpointExtensions
{
    /// <summary>
    /// Maps all commerce endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapCartEndpoints(apiVersionSet);
        app.MapAccountEndpoints(apiVersionSet);

        return app;
    }
}
