using HAMBOX.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Application.Extensions;

/// <summary>
/// Provides dependency injection registration helpers for the application layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared application pipeline behaviors.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddApplicationBuildingBlocks(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
