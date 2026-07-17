using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Communication.Domain.Communication;

/// <summary>
/// Configuration row for a registered <c>ICommunicationProvider</c>. Mirrors <c>Supplier</c>: the
/// platform core only ever depends on <c>ICommunicationProvider</c>/<c>ICommunicationProviderRegistry</c>
/// (Application layer) — this entity merely stores which provider handles a channel and whether it's
/// enabled, resolved into a provider instance at runtime via <see cref="ProviderType"/>.
/// </summary>
public sealed class CommunicationProviderConfig : AggregateRoot, IAuditable
{
    private CommunicationProviderConfig()
    {
    }

    private CommunicationProviderConfig(Guid id, string name, string providerType, string channel, int priority)
        : base(id)
    {
        Name = name;
        ProviderType = providerType;
        Channel = channel;
        Priority = priority;
        IsEnabled = true;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Key of the registered <c>ICommunicationProvider</c> resolved by <c>ICommunicationProviderRegistry</c>.</summary>
    public string ProviderType { get; private set; } = string.Empty;

    public string Channel { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static CommunicationProviderConfig Create(string name, string providerType, string channel, int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        return new CommunicationProviderConfig(Guid.NewGuid(), name.Trim(), providerType.Trim(), channel.Trim(), priority);
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void UpdatePriority(int priority) => Priority = priority;
}
