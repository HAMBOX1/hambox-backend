using Asp.Versioning;
using HAMBOX.Modules.Catalog.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Catalog.Presentation.Extensions;

/// <summary>
/// Extension methods for catalog endpoints.
/// </summary>
public static class CatalogEndpointExtensions
{
    /// <summary>
    /// Maps all catalog endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapCategoryEndpoints(apiVersionSet);
        app.MapCategoryImageEndpoints(apiVersionSet);
        app.MapCollectionEndpoints(apiVersionSet);
        app.MapProductEndpoints(apiVersionSet);
        app.MapProductImageEndpoints(apiVersionSet);
        app.MapProductInstructionsEndpoints(apiVersionSet);
        app.MapStorefrontEndpoints(apiVersionSet);
        app.MapInventoryEndpoints(apiVersionSet);
        app.MapCatalogImportExportEndpoints(apiVersionSet);

        return app;
    }
}
