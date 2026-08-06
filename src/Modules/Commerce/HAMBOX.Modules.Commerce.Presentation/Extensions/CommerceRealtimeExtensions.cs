using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Presentation.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Commerce.Presentation.Extensions;

public static class CommerceRealtimeExtensions
{
    public static IServiceCollection AddCommerceRealtime(this IServiceCollection services)
    {
        // AddSignalR() is safe to call more than once (its internal registrations are TryAdd-based)
        // — calling it here too keeps this hub self-sufficient rather than silently depending on the
        // Support module's AddSignalR() call having already run.
        services.AddSignalR();
        services.AddScoped<IUserNotificationRealtimeNotifier, SignalRUserNotificationRealtimeNotifier>();
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<NotificationHub>("/hubs/notifications");
        return app;
    }
}
