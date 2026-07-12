/// <summary>
/// Persisted JSON payload for a platform settings category.
/// </summary>
public sealed class PlatformSettingsCategory
{
    private PlatformSettingsCategory()
    {
    }

    private PlatformSettingsCategory(string categoryKey, string payloadJson)
    {
        CategoryKey = categoryKey;
        PayloadJson = payloadJson;
        ModifiedOnUtc = DateTimeOffset.UtcNow;
    }

    public string CategoryKey { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset ModifiedOnUtc { get; private set; }

    public string? ModifiedByUserId { get; private set; }

    public static PlatformSettingsCategory Create(string categoryKey, string payloadJson) =>
        new(categoryKey, payloadJson);

    public void UpdatePayload(string payloadJson, string? modifiedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        PayloadJson = payloadJson;
        ModifiedByUserId = modifiedByUserId;
        ModifiedOnUtc = DateTimeOffset.UtcNow;
    }
}
