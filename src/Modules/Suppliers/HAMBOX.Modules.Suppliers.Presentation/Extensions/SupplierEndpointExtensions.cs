using Asp.Versioning;
using HAMBOX.Modules.Suppliers.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Suppliers.Presentation.Extensions;

public static class SupplierEndpointExtensions
{
    public static IEndpointRouteBuilder MapSuppliersEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapSupplierEndpoints(apiVersionSet);
        return app;
    }
}
