namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// Architecture-only scaffold for future device blocking. No fingerprinting logic exists yet —
/// this entity exists so the schema, DbSet, and configuration are in place for when a device
/// fingerprint source is chosen. No service, endpoint, or permission reads/writes this today.
/// </summary>
public sealed class BlockedDevice : BlockListEntry
{
    private BlockedDevice()
    {
    }

    private BlockedDevice(Guid id, string deviceFingerprint, string reason, string? notes, DateTimeOffset? expiresOnUtc)
        : base(id, reason, notes, expiresOnUtc)
    {
        DeviceFingerprint = deviceFingerprint;
    }

    /// <summary>
    /// Gets the opaque device fingerprint identifier this entry blocks.
    /// </summary>
    public string DeviceFingerprint { get; private set; } = string.Empty;

    public static BlockedDevice Create(string deviceFingerprint, string reason, string? notes = null, DateTimeOffset? expiresOnUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFingerprint);

        return new BlockedDevice(Guid.NewGuid(), deviceFingerprint, reason, notes, expiresOnUtc);
    }
}
