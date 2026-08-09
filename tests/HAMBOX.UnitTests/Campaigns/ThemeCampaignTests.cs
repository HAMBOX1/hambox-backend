using HAMBOX.Modules.Themes.Domain.Campaigns;

namespace HAMBOX.UnitTests.Campaigns;

public class ThemeCampaignTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc);

    private static ThemeCampaign CreateCampaign(int priority = 0) =>
        ThemeCampaign.Create("Black Friday 2026", "Storewide sale", Guid.NewGuid(), Start, End, priority);

    // ── Creation validation ─────────────────────────────────────

    [Fact]
    public void Create_ValidInputs_StartsAsDraft()
    {
        var campaign = CreateCampaign();

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.True(campaign.IsEnabled);
        Assert.False(campaign.IsDeleted);
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ThemeCampaign.Create("", null, Guid.NewGuid(), Start, End));
    }

    [Fact]
    public void Create_NonUtcStart_Throws()
    {
        var localStart = DateTime.SpecifyKind(Start, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            ThemeCampaign.Create("Black Friday", null, Guid.NewGuid(), localStart, End));
    }

    [Fact]
    public void Create_NonUtcEnd_Throws()
    {
        var unspecifiedEnd = DateTime.SpecifyKind(End, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() =>
            ThemeCampaign.Create("Black Friday", null, Guid.NewGuid(), Start, unspecifiedEnd));
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ThemeCampaign.Create("Black Friday", null, Guid.NewGuid(), Start, Start.AddDays(-1)));
    }

    [Fact]
    public void Create_EndEqualsStart_Throws()
    {
        // EndsAtUtc is required and must be strictly after StartsAtUtc — a zero-length window
        // isn't a real campaign.
        Assert.Throws<ArgumentException>(() =>
            ThemeCampaign.Create("Black Friday", null, Guid.NewGuid(), Start, Start));
    }

    [Fact]
    public void Reschedule_NonUtcDates_Throws()
    {
        var campaign = CreateCampaign();

        Assert.Throws<ArgumentException>(() =>
            campaign.Reschedule(DateTime.SpecifyKind(Start, DateTimeKind.Local), End, 0));
    }

    // ── Resolution eligibility (IsEffectiveAt) ──────────────────

    [Fact]
    public void IsEffectiveAt_DraftCampaign_NeverResolves()
    {
        var campaign = CreateCampaign();

        Assert.False(campaign.IsEffectiveAt(Start.AddHours(1)));
    }

    [Fact]
    public void IsEffectiveAt_PublishedEnabledWithinWindow_Resolves()
    {
        var campaign = CreateCampaign();
        campaign.Publish();

        Assert.True(campaign.IsEffectiveAt(Start));
        Assert.True(campaign.IsEffectiveAt(Start.AddDays(1)));
    }

    [Fact]
    public void IsEffectiveAt_BeforeStart_DoesNotResolve()
    {
        var campaign = CreateCampaign();
        campaign.Publish();

        Assert.False(campaign.IsEffectiveAt(Start.AddSeconds(-1)));
    }

    [Fact]
    public void IsEffectiveAt_AtOrAfterEnd_DoesNotResolve()
    {
        var campaign = CreateCampaign();
        campaign.Publish();

        Assert.False(campaign.IsEffectiveAt(End));
        Assert.False(campaign.IsEffectiveAt(End.AddDays(1)));
    }

    [Fact]
    public void IsEffectiveAt_Disabled_DoesNotResolve()
    {
        var campaign = CreateCampaign();
        campaign.Publish();
        campaign.Disable();

        Assert.False(campaign.IsEffectiveAt(Start.AddDays(1)));
    }

    [Fact]
    public void IsEffectiveAt_Archived_DoesNotResolve()
    {
        var campaign = CreateCampaign();
        campaign.Publish();
        campaign.Archive();

        Assert.False(campaign.IsEffectiveAt(Start.AddDays(1)));
    }

    [Fact]
    public void IsEffectiveAt_ReEnabledAfterDisable_ResolvesAgainWithinWindow()
    {
        var campaign = CreateCampaign();
        campaign.Publish();
        campaign.Disable();
        campaign.Enable();

        Assert.True(campaign.IsEffectiveAt(Start.AddDays(1)));
    }

    // ── Computed phase (display only) ───────────────────────────

    [Fact]
    public void GetPhase_Draft_ReturnsDraft()
    {
        var campaign = CreateCampaign();

        Assert.Equal(CampaignPhase.Draft, campaign.GetPhase(Now));
    }

    [Fact]
    public void GetPhase_PublishedBeforeStart_ReturnsScheduled()
    {
        var campaign = CreateCampaign();
        campaign.Publish();

        Assert.Equal(CampaignPhase.Scheduled, campaign.GetPhase(Now));
    }

    [Fact]
    public void GetPhase_PublishedWithinWindow_ReturnsActive()
    {
        var campaign = CreateCampaign();
        campaign.Publish();

        Assert.Equal(CampaignPhase.Active, campaign.GetPhase(Start.AddDays(1)));
    }

    [Fact]
    public void GetPhase_PublishedAfterEnd_ReturnsEnded()
    {
        var campaign = CreateCampaign();
        campaign.Publish();

        Assert.Equal(CampaignPhase.Ended, campaign.GetPhase(End.AddDays(1)));
    }

    [Fact]
    public void GetPhase_DisabledRegardlessOfWindow_ReturnsPaused()
    {
        var campaign = CreateCampaign();
        campaign.Publish();
        campaign.Disable();

        Assert.Equal(CampaignPhase.Paused, campaign.GetPhase(Start.AddDays(1)));
    }

    [Fact]
    public void GetPhase_Archived_ReturnsArchivedRegardlessOfWindow()
    {
        var campaign = CreateCampaign();
        campaign.Publish();
        campaign.Archive();

        Assert.Equal(CampaignPhase.Archived, campaign.GetPhase(Start.AddDays(1)));
    }
}
