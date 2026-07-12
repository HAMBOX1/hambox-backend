using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class CheckoutConfigurationProvider(IHostEnvironment environment) : ICheckoutConfigurationProvider
{
    public bool IsDevelopmentCheckoutEnabled => environment.IsDevelopment();
}
