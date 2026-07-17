using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Services;

/// <summary>
/// Resolves by <see cref="ISupplierProvider.ProviderType"/> over whatever <see cref="ISupplierProvider"/>
/// instances are registered in DI — the same lookup-by-key shape as Commerce's PaymentProviderResolver.
/// Adding a new supplier provider is exactly: implement <see cref="ISupplierProvider"/>, register it in
/// <see cref="Extensions.SuppliersInfrastructureExtensions"/>. This class never changes.
/// </summary>
internal sealed class SupplierProviderRegistry(IEnumerable<ISupplierProvider> providers) : ISupplierProviderRegistry
{
    public Result<ISupplierProvider> Resolve(string providerType)
    {
        var provider = providers.FirstOrDefault(p => string.Equals(p.ProviderType, providerType, StringComparison.OrdinalIgnoreCase));
        return provider is null
            ? Result.Failure<ISupplierProvider>(SupplierErrors.ProviderTypeNotRegistered)
            : Result.Success(provider);
    }

    public IReadOnlyCollection<string> GetRegisteredProviderTypes() =>
        providers.Select(p => p.ProviderType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
