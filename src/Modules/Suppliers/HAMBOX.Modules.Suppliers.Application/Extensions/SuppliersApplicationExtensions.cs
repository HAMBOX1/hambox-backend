using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Suppliers.Application.Extensions;

/// <summary>
/// Mirrors Commerce's <c>AddCommerceApplication()</c> — registration for Application-layer services
/// that are more than a MediatR handler (auto-discovered separately) or a validator (assembly-scanned
/// separately). Called once from <c>Program.cs</c> alongside <c>AddSuppliersInfrastructure</c>.
/// </summary>
public static class SuppliersApplicationExtensions
{
    public static IServiceCollection AddSuppliersApplication(this IServiceCollection services)
    {
        services.AddScoped<ISupplierFulfillmentService, SupplierFulfillmentService>();
        services.AddScoped<ISupplierAvailabilityService, SupplierAvailabilityService>();

        return services;
    }
}
