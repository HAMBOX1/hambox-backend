using System.Text.Json.Serialization;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Presentation.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Support.Presentation.Extensions;

public static class SupportRealtimeExtensions
{
    public static IServiceCollection AddSupportRealtime(this IServiceCollection services)
    {
        // SignalR's hub JSON protocol has its own JsonSerializerOptions, entirely separate from
        // ConfigureHttpJsonOptions (which is what makes REST responses serialize enums as
        // strings). Without this, TicketMessageAuthorRole/TicketStatus go over the wire as raw
        // ints on the SignalR channel while REST sends the same enums as strings — the frontend's
        // roleFromApi()/statusFromApi() mappers only understand the string form.
        services.AddSignalR().AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddSingleton<IUserIdProvider, SupportUserIdProvider>();
        services.AddScoped<ISupportRealtimeNotifier, SignalRSupportRealtimeNotifier>();
        return services;
    }

    public static IEndpointRouteBuilder MapSupportHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<SupportHub>("/hubs/support");
        return app;
    }
}
