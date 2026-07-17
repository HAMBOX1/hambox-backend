using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Communication.Application.Abstractions;

/// <summary>
/// Resolves by <see cref="ICommunicationProvider.ChannelKey"/> over whatever <see cref="ICommunicationProvider"/>
/// instances are registered in DI — the same lookup-by-key shape as Suppliers' <c>ISupplierProviderRegistry</c>.
/// This class never changes when a new channel provider is added.
/// </summary>
public interface ICommunicationProviderRegistry
{
    Result<ICommunicationProvider> Resolve(string channelKey);

    IReadOnlyCollection<string> GetRegisteredChannelKeys();
}
