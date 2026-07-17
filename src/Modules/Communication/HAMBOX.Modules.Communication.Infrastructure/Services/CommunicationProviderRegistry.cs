using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// Resolves by <see cref="ICommunicationProvider.ChannelKey"/> over whatever providers are registered in
/// DI. Adding a future channel (Sms, WhatsApp, ...) is exactly: implement <see cref="ICommunicationProvider"/>,
/// register it in <see cref="Extensions.CommunicationInfrastructureExtensions"/>. This class never changes.
/// </summary>
internal sealed class CommunicationProviderRegistry(IEnumerable<ICommunicationProvider> providers) : ICommunicationProviderRegistry
{
    public Result<ICommunicationProvider> Resolve(string channelKey)
    {
        var provider = providers.FirstOrDefault(p => string.Equals(p.ChannelKey, channelKey, StringComparison.OrdinalIgnoreCase));
        return provider is null
            ? Result.Failure<ICommunicationProvider>(CommunicationErrors.ChannelNotRegistered)
            : Result.Success(provider);
    }

    public IReadOnlyCollection<string> GetRegisteredChannelKeys() =>
        providers.Select(p => p.ChannelKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
