using Asp.Versioning;
using HAMBOX.Modules.Legal.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Legal.Presentation.Extensions;

public static class LegalEndpointExtensions
{
    public static IEndpointRouteBuilder MapLegalEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapLegalDocumentEndpoints(apiVersionSet);
        return app;
    }
}
