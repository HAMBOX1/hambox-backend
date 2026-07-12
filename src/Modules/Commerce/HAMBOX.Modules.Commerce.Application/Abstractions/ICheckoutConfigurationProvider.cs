namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface ICheckoutConfigurationProvider
{
    bool IsDevelopmentCheckoutEnabled { get; }
}
