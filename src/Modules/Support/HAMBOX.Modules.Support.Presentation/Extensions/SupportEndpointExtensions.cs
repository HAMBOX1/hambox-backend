using Asp.Versioning;
using HAMBOX.Modules.Support.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Support.Presentation.Extensions;

public static class SupportEndpointExtensions
{
    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapTicketEndpoints(apiVersionSet);
        app.MapLookupEndpoints(apiVersionSet);
        app.MapKnowledgeBaseEndpoints(apiVersionSet);
        app.MapSavedReplyEndpoints(apiVersionSet);
        app.MapAnalyticsEndpoints(apiVersionSet);
        return app;
    }
}
