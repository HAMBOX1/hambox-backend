using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// Resolves the <see cref="ISupplierProvider"/> registered for a given <c>Supplier.ProviderType</c>.
/// The Infrastructure implementation is a lookup over DI-registered <see cref="ISupplierProvider"/>
/// instances keyed by <see cref="ISupplierProvider.ProviderType"/> — adding a new provider is purely
/// "implement the interface + register it in DI"; nothing here or in any caller branches on type.
/// </summary>
public interface ISupplierProviderRegistry
{
    Result<ISupplierProvider> Resolve(string providerType);

    IReadOnlyCollection<string> GetRegisteredProviderTypes();
}
