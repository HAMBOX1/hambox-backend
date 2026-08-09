using HAMBOX.Application.Abstractions;
using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Themes.Application.Contracts.Campaigns;
using HAMBOX.Modules.Themes.Application.Errors;
using HAMBOX.Modules.Themes.Application.Features.Campaigns;
using HAMBOX.Modules.Themes.Application.Services;
using HAMBOX.Modules.Themes.Domain.Campaigns;
using HAMBOX.Modules.Themes.Domain.Themes;
using HAMBOX.Modules.Themes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Campaigns;

/// <summary>
/// Covers the campaign lifecycle and ThemeEngine resolution precedence end to end against a real
/// <see cref="ThemesDbContext"/> backed by file-based SQLite — same harness pattern as
/// ReferralLifecycleServiceTests, for the same reason (two genuinely separate connections need to
/// race against the same database for the concurrency test).
/// </summary>
public sealed class CampaignLifecycleTests : IDisposable
{
    private readonly List<string> _tempDatabases = [];

    private string CreateDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hambox-campaign-test-{Guid.NewGuid():N}.db");
        _tempDatabases.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempDatabases)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // best effort — a lingering handle on a temp file is not worth failing the test run over
            }
        }
    }

    private static ThemesDbContext CreateDbContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<ThemesDbContext>()
            .UseSqlite($"Data Source={dbPath};Default Timeout=5")
            .Options;

        var db = new ThemesDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public string? UserId => "test-admin";
        public bool IsAuthenticated => true;
    }

    private static async Task<StoreTheme> SeedThemeAsync(ThemesDbContext db, string slug, bool published = true)
    {
        var theme = StoreTheme.Create($"{slug} theme", slug, null, ThemeBaseMode.Dark);
        var version = theme.CreateDraftVersion(new Dictionary<string, string> { ["primary"] = "#000000" });
        if (published)
        {
            theme.PublishVersion(version.Id);
        }

        db.StoreThemes.Add(theme);
        await db.SaveChangesAsync();
        return theme;
    }

    /// <summary>Directly sets CreatedOnUtc for deterministic tiebreak testing — this harness has no
    /// AuditInterceptor wired in (same as ReferralLifecycleServiceTests), so it would otherwise stay
    /// at its default value for every row.</summary>
    private static void SetCreatedOnUtc(ThemesDbContext db, ThemeCampaign campaign, DateTimeOffset createdOnUtc) =>
        db.Entry(campaign).Property(nameof(BaseEntity.CreatedOnUtc)).CurrentValue = createdOnUtc;

    private static readonly DateTime WindowStart = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DuringWindow = new(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);

    // ── Publish gate ─────────────────────────────────────────────

    [Fact]
    public async Task PublishCampaign_ThemeIsDraft_ReturnsThemeNotPublishable()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "draft-theme", published: false);
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var handler = new PublishCampaignCommandHandler(db, new FakeCurrentUser());
        var result = await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CampaignErrors.ThemeNotPublishable.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublishCampaign_ThemeIsPublished_Succeeds()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "published-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var handler = new PublishCampaignCommandHandler(db, new FakeCurrentUser());
        var result = await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var reloaded = await db.ThemeCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaign.Id);
        Assert.Equal(CampaignStatus.Published, reloaded.Status);
    }

    // ── Resolution eligibility ───────────────────────────────────

    [Fact]
    public async Task Resolve_DraftCampaign_DoesNotResolve()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.Null(active);
    }

    [Fact]
    public async Task Resolve_DisabledCampaign_DoesNotResolve()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        campaign.Disable();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.Null(active);
    }

    [Fact]
    public async Task Resolve_ExpiredCampaign_DoesNotResolve()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: WindowEnd.AddDays(1));

        Assert.Null(active);
    }

    [Fact]
    public async Task Resolve_ArchivedCampaign_DoesNotResolve()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        campaign.Archive();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.Null(active);
    }

    [Fact]
    public async Task Resolve_DeletedCampaign_DoesNotResolve()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        campaign.IsDeleted = true;
        campaign.DeletedOnUtc = DateTimeOffset.UtcNow;
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.Null(active);
    }

    [Fact]
    public async Task Resolve_ActiveCampaign_Resolves()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.NotNull(active);
        Assert.Equal(theme.Id, active!.ThemeId);
        Assert.Equal("campaign", active.ResolutionSource);
    }

    // ── Precedence over other sources ────────────────────────────

    [Fact]
    public async Task Resolve_CampaignOverridesMembership()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var membershipTheme = await SeedThemeAsync(db, "gold-theme");
        membershipTheme.AddAssignment(ThemeAssignmentType.Membership, "gold", priority: 10);
        db.ThemeAssignments.AddRange(membershipTheme.Assignments);

        var campaignTheme = await SeedThemeAsync(db, "black-friday-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, campaignTheme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: "gold-member", membershipPlanSlug: "gold", asOfUtc: DuringWindow);

        Assert.NotNull(active);
        Assert.Equal(campaignTheme.Id, active!.ThemeId);
        Assert.Equal("campaign", active.ResolutionSource);
    }

    [Fact]
    public async Task Resolve_MembershipWinsAfterCampaignEnds()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var membershipTheme = await SeedThemeAsync(db, "gold-theme");
        membershipTheme.AddAssignment(ThemeAssignmentType.Membership, "gold", priority: 10);
        db.ThemeAssignments.AddRange(membershipTheme.Assignments);

        var campaignTheme = await SeedThemeAsync(db, "black-friday-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, campaignTheme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var afterCampaign = await engine.ResolveActiveThemeAsync(userId: "gold-member", membershipPlanSlug: "gold", asOfUtc: WindowEnd.AddMinutes(1));

        Assert.NotNull(afterCampaign);
        Assert.Equal(membershipTheme.Id, afterCampaign!.ThemeId);
        Assert.Equal("membership", afterCampaign.ResolutionSource);
    }

    [Fact]
    public async Task Resolve_CampaignOverridesSchedule()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var scheduledTheme = await SeedThemeAsync(db, "scheduled-theme");
        scheduledTheme.AddSchedule(WindowStart.AddDays(-5), WindowEnd.AddDays(5));
        db.ThemeSchedules.AddRange(scheduledTheme.Schedules);

        var campaignTheme = await SeedThemeAsync(db, "black-friday-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, campaignTheme.Id, WindowStart, WindowEnd);
        campaign.Publish();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.NotNull(active);
        Assert.Equal(campaignTheme.Id, active!.ThemeId);
        Assert.Equal("campaign", active.ResolutionSource);
    }

    // ── Overlapping campaigns: deterministic tiebreak ────────────

    [Fact]
    public async Task Resolve_OverlappingCampaigns_HigherPriorityWins()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var lowPriorityTheme = await SeedThemeAsync(db, "low-priority-theme");
        var highPriorityTheme = await SeedThemeAsync(db, "high-priority-theme");

        var low = ThemeCampaign.Create("Low", null, lowPriorityTheme.Id, WindowStart, WindowEnd, priority: 1);
        low.Publish();
        var high = ThemeCampaign.Create("High", null, highPriorityTheme.Id, WindowStart, WindowEnd, priority: 10);
        high.Publish();
        db.ThemeCampaigns.AddRange(low, high);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        Assert.Equal(highPriorityTheme.Id, active!.ThemeId);
    }

    [Fact]
    public async Task Resolve_OverlappingCampaigns_SamePriority_LaterStartWins()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var earlierTheme = await SeedThemeAsync(db, "earlier-theme");
        var laterTheme = await SeedThemeAsync(db, "later-theme");

        var earlier = ThemeCampaign.Create("Earlier start", null, earlierTheme.Id, WindowStart, WindowEnd, priority: 5);
        earlier.Publish();
        var later = ThemeCampaign.Create("Later start", null, laterTheme.Id, WindowStart.AddHours(1), WindowEnd, priority: 5);
        later.Publish();
        db.ThemeCampaigns.AddRange(earlier, later);
        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: WindowStart.AddHours(2));

        Assert.Equal(laterTheme.Id, active!.ThemeId);
    }

    [Fact]
    public async Task Resolve_OverlappingCampaigns_SamePriorityAndStart_EarlierCreatedWins()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);

        var firstCreatedTheme = await SeedThemeAsync(db, "first-created-theme");
        var secondCreatedTheme = await SeedThemeAsync(db, "second-created-theme");

        var firstCreated = ThemeCampaign.Create("First created", null, firstCreatedTheme.Id, WindowStart, WindowEnd, priority: 5);
        firstCreated.Publish();
        db.ThemeCampaigns.Add(firstCreated);
        await db.SaveChangesAsync();
        SetCreatedOnUtc(db, firstCreated, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var secondCreated = ThemeCampaign.Create("Second created", null, secondCreatedTheme.Id, WindowStart, WindowEnd, priority: 5);
        secondCreated.Publish();
        db.ThemeCampaigns.Add(secondCreated);
        await db.SaveChangesAsync();
        SetCreatedOnUtc(db, secondCreated, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        await db.SaveChangesAsync();

        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null, asOfUtc: DuringWindow);

        // Same priority, same start — the campaign created first (the earlier admin action) wins.
        Assert.Equal(firstCreatedTheme.Id, active!.ThemeId);
    }

    // ── Concurrency ───────────────────────────────────────────────

    [Fact]
    public async Task PublishCampaign_ConcurrentConflict_ReturnsCleanResultError()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, WindowStart, WindowEnd);
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        // Simulate another admin's write already having happened: corrupt this context's
        // understanding of the row's original RowVersion so EF's optimistic-concurrency WHERE
        // clause matches zero rows on save, regardless of provider-specific rowversion semantics.
        db.Entry(campaign).Property("RowVersion").OriginalValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var handler = new PublishCampaignCommandHandler(db, new FakeCurrentUser());
        var result = await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CampaignErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    // ── Resolved-winner vs. merely-Active (audit findings H1 / M1) ──

    // GetCampaignsQueryHandler/GetCampaignByIdQueryHandler use real DateTime.UtcNow internally
    // (unlike ThemeEngine.ResolveActiveThemeAsync, which accepts an asOfUtc override) — so these
    // tests need a window that genuinely contains the real current instant, not the fixed
    // 2026-11-27..30 WindowStart/WindowEnd constants used everywhere else in this file (those
    // rely on an explicit asOfUtc and would otherwise land in the future relative to "now").
    private static (DateTime Start, DateTime End) CurrentWindow()
    {
        var now = DateTime.UtcNow;
        return (now.AddHours(-1), now.AddHours(1));
    }

    [Fact]
    public async Task GetCampaigns_TwoOverlappingActiveCampaigns_OnlyHigherPriorityIsResolvedWinner()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var (start, end) = CurrentWindow();

        var loserTheme = await SeedThemeAsync(db, "low-priority-theme");
        var winnerTheme = await SeedThemeAsync(db, "high-priority-theme");

        var loser = ThemeCampaign.Create("Low Priority Sale", null, loserTheme.Id, start, end, priority: 1);
        loser.Publish();
        var winner = ThemeCampaign.Create("Black Friday", null, winnerTheme.Id, start, end, priority: 10);
        winner.Publish();
        db.ThemeCampaigns.AddRange(loser, winner);
        await db.SaveChangesAsync();

        var handler = new GetCampaignsQueryHandler(db);
        var result = await handler.Handle(new GetCampaignsQuery(1, 20, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var loserDto = result.Value.Items.Single(c => c.Id == loser.Id);
        var winnerDto = result.Value.Items.Single(c => c.Id == winner.Id);

        // Both are within their own window, so CampaignPhase.GetPhase independently reports
        // "Active" for both (phase semantics are unchanged) — but only one is IsResolvedWinner,
        // matching exactly what ThemeEngine.ResolveActiveCampaignThemeIdAsync would pick.
        Assert.Equal("Active", loserDto.Phase);
        Assert.Equal("Active", winnerDto.Phase);
        Assert.True(winnerDto.IsResolvedWinner);
        Assert.False(loserDto.IsResolvedWinner);
        Assert.Equal(winner.Name, loserDto.OverriddenByCampaignName);
        Assert.Null(winnerDto.OverriddenByCampaignName);

        // Cross-check against the real resolver directly — the admin-facing winner must be the
        // same theme ThemeEngine actually serves to the storefront.
        var engine = new ThemeEngine(db);
        var active = await engine.ResolveActiveThemeAsync(userId: null, membershipPlanSlug: null);
        Assert.Equal(winnerTheme.Id, active!.ThemeId);
    }

    [Fact]
    public async Task GetCampaignById_NotTheResolvedWinner_ExposesOverriddenByName()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var (start, end) = CurrentWindow();

        var loserTheme = await SeedThemeAsync(db, "low-priority-theme");
        var winnerTheme = await SeedThemeAsync(db, "high-priority-theme");

        var loser = ThemeCampaign.Create("Low Priority Sale", null, loserTheme.Id, start, end, priority: 1);
        loser.Publish();
        var winner = ThemeCampaign.Create("Black Friday", null, winnerTheme.Id, start, end, priority: 10);
        winner.Publish();
        db.ThemeCampaigns.AddRange(loser, winner);
        await db.SaveChangesAsync();

        var handler = new GetCampaignByIdQueryHandler(db);
        var result = await handler.Handle(new GetCampaignByIdQuery(loser.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsResolvedWinner);
        Assert.Equal(winner.Name, result.Value.OverriddenByCampaignName);
    }

    [Fact]
    public async Task GetCampaigns_SinglePublishedCampaign_IsResolvedWinnerAndNotOverridden()
    {
        var dbPath = CreateDatabasePath();
        using var db = CreateDbContext(dbPath);
        var (start, end) = CurrentWindow();
        var theme = await SeedThemeAsync(db, "campaign-theme");
        var campaign = ThemeCampaign.Create("Black Friday", null, theme.Id, start, end);
        campaign.Publish();
        db.ThemeCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var handler = new GetCampaignsQueryHandler(db);
        var result = await handler.Handle(new GetCampaignsQuery(1, 20, null, null), CancellationToken.None);

        var dto = result.Value.Items.Single();
        Assert.True(dto.IsResolvedWinner);
        Assert.Null(dto.OverriddenByCampaignName);
    }
}
