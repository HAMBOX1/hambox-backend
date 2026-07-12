using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemeAsset : BaseEntity
{
    private ThemeAsset()
    {
    }

    private ThemeAsset(Guid id, Guid themeId, ThemeAssetType assetType, string url, string? altText)
        : base(id)
    {
        ThemeId = themeId;
        AssetType = assetType;
        Url = url;
        AltText = altText;
    }

    public Guid ThemeId { get; private set; }
    public ThemeAssetType AssetType { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? AltText { get; private set; }

    public static ThemeAsset Create(Guid themeId, ThemeAssetType assetType, string url, string? altText) =>
        new(Guid.NewGuid(), themeId, assetType, url, altText);

    public void Update(string url, string? altText)
    {
        Url = url;
        AltText = altText;
    }
}
