using HAMBOX.Modules.Themes.Application.Services;
using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.UnitTests.Campaigns;

/// <summary>
/// Covers CampaignMapper.IsThemePublishable — the owner-safety rule that a campaign must not be
/// publishable while pointing at a theme that isn't itself Published. The full gate (including the
/// DB lookup) lives in PublishCampaignCommandHandler; this is the pure decision logic underneath it.
/// </summary>
public class CampaignMapperTests
{
    private static StoreTheme CreateTheme(ThemeBaseMode mode = ThemeBaseMode.Dark) =>
        StoreTheme.Create("Gold Membership", "gold-membership", null, mode);

    [Fact]
    public void IsThemePublishable_PublishedTheme_ReturnsTrue()
    {
        var theme = CreateTheme();
        var version = theme.CreateDraftVersion(new Dictionary<string, string> { ["primary"] = "#000" });
        theme.PublishVersion(version.Id);

        Assert.True(CampaignMapper.IsThemePublishable(theme));
    }

    [Fact]
    public void IsThemePublishable_DraftTheme_ReturnsFalse()
    {
        var theme = CreateTheme();
        theme.CreateDraftVersion(new Dictionary<string, string> { ["primary"] = "#000" });

        Assert.False(CampaignMapper.IsThemePublishable(theme));
    }

    [Fact]
    public void IsThemePublishable_ArchivedTheme_ReturnsFalse()
    {
        var theme = CreateTheme();
        var version = theme.CreateDraftVersion(new Dictionary<string, string> { ["primary"] = "#000" });
        theme.PublishVersion(version.Id);
        theme.Archive();

        Assert.False(CampaignMapper.IsThemePublishable(theme));
    }

    [Fact]
    public void IsThemePublishable_MissingTheme_ReturnsFalse()
    {
        Assert.False(CampaignMapper.IsThemePublishable(null));
    }
}
