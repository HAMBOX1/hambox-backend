using Asp.Versioning;
using HAMBOX.Modules.Communication.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Communication.Presentation.Extensions;

public static class CommunicationEndpointExtensions
{
    public static IEndpointRouteBuilder MapCommunicationEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapCommunicationAdminEndpoints(apiVersionSet);
        app.MapCommunicationPreferenceEndpoints(apiVersionSet);
        return app;
    }
}
