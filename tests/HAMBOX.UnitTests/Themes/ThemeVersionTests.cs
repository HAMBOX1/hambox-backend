using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.UnitTests.Themes;

public class ThemeVersionTests
{
    private static ThemeVersion CreateDraft() =>
        ThemeVersion.CreateDraft(Guid.NewGuid(), 1, new Dictionary<string, string> { ["primary"] = "#000000" }, null);

    [Fact]
    public void Publish_SetsIsPublishedAndHasEverBeenPublished()
    {
        var version = CreateDraft();

        version.Publish();

        Assert.True(version.IsPublished);
        Assert.True(version.HasEverBeenPublished);
    }

    [Fact]
    public void Unpublish_ClearsIsPublished_ButHasEverBeenPublishedStaysTrue()
    {
        // This is the exact bug the rollback fix depends on: PublishVersion() unpublishes the
        // previously-live version on every new publish, so HasEverBeenPublished — not IsPublished —
        // must be the permanent record that a version was ever live.
        var version = CreateDraft();
        version.Publish();

        version.Unpublish();

        Assert.False(version.IsPublished);
        Assert.True(version.HasEverBeenPublished);
    }

    [Fact]
    public void UpdateTokens_OnUnpublishedDraft_Succeeds()
    {
        var version = CreateDraft();

        version.UpdateTokens(new Dictionary<string, string> { ["primary"] = "#111111" });

        Assert.Equal("#111111", version.GetTokens()["primary"]);
    }

    [Fact]
    public void UpdateTokens_AfterPublish_Throws()
    {
        var version = CreateDraft();
        version.Publish();

        Assert.Throws<InvalidOperationException>(() =>
            version.UpdateTokens(new Dictionary<string, string> { ["primary"] = "#222222" }));
    }

    [Fact]
    public void UpdateTokens_AfterUnpublish_StillThrows()
    {
        // A superseded (unpublished) version was live in production at some point and must remain
        // immutable forever, not just while it holds the current live slot.
        var version = CreateDraft();
        version.Publish();
        version.Unpublish();

        Assert.Throws<InvalidOperationException>(() =>
            version.UpdateTokens(new Dictionary<string, string> { ["primary"] = "#333333" }));
    }
}
