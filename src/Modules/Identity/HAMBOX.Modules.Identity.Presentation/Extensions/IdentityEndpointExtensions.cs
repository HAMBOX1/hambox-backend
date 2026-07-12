using Asp.Versioning;
using HAMBOX.Modules.Identity.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Identity.Presentation.Extensions;

/// <summary>
/// Extension methods for mapping identity endpoints in the Presentation layer.
/// </summary>
public static class IdentityEndpointExtensions
{
    /// <summary>
    /// Maps the identity endpoints to the API host routing configuration.
    /// </summary>
    /// <param name="builder">The route builder.</param>
    /// <returns>The updated route builder.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder builder)
    {
        var apiVersionSet = builder.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        builder.MapAuthEndpoints();
        builder.MapRoleEndpoints(apiVersionSet);
        builder.MapSettingsEndpoints(apiVersionSet);
        return builder;
    }
}
