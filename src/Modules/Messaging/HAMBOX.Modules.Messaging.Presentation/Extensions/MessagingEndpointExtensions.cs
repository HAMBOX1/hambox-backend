using Asp.Versioning;
using HAMBOX.Modules.Messaging.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Messaging.Presentation.Extensions;

public static class MessagingEndpointExtensions
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapWhatsAppWebhookEndpoints();
        app.MapWhatsAppBotConfigurationEndpoints(apiVersionSet);
        return app;
    }
}
