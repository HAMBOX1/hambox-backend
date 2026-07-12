using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemePreviewSession : BaseEntity
{
    private ThemePreviewSession()
    {
    }

    private ThemePreviewSession(Guid id, Guid themeId, Guid versionId, string token, DateTime expiresAtUtc, string? createdByUserId)
        : base(id)
    {
        ThemeId = themeId;
        VersionId = versionId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public Guid ThemeId { get; private set; }
    public Guid VersionId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public string? CreatedByUserId { get; private set; }

    public static ThemePreviewSession Create(Guid themeId, Guid versionId, string? createdByUserId, TimeSpan ttl)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new ThemePreviewSession(
            Guid.NewGuid(),
            themeId,
            versionId,
            token,
            DateTime.UtcNow.Add(ttl),
            createdByUserId);
    }

    public bool IsValid(DateTime utcNow) => utcNow <= ExpiresAtUtc;
}
