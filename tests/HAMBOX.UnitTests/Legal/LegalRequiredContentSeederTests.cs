using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Domain.Legal;
using HAMBOX.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.UnitTests.Legal;

/// <summary>
/// Proves <see cref="LegalRequiredContentSeeder"/> gets the four ADM-44/45 sections into a state
/// <c>LegalAcceptanceRecorder</c> can actually act on (real content, published), without disturbing a
/// section an admin has already published content for.
/// </summary>
public sealed class LegalRequiredContentSeederTests
{
    private static readonly string[] RequiredSlugs = ["terms", "privacy", "refund", "delivery"];

    private static (IServiceProvider Services, LegalDbContext Db) BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<LegalDbContext>(o => o.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<LegalDbContext>());
    }

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesAllFourSectionsWithPublishedRealContent()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        await LegalRequiredContentSeeder.SeedAsync(services);

        var sections = await db.LegalSections.Include(s => s.Versions).ToListAsync();
        Assert.Equal(4, sections.Count);

        foreach (var slug in RequiredSlugs)
        {
            var section = Assert.Single(sections, s => s.Slug == slug);
            Assert.True(section.RequireAcceptance);
            Assert.NotNull(section.PublishedVersionId);

            var published = section.Versions.Single(v => v.Id == section.PublishedVersionId);
            Assert.True(published.IsPublished);
            Assert.False(string.IsNullOrWhiteSpace(published.ContentEn));
            Assert.False(string.IsNullOrWhiteSpace(published.TitleEn));
        }
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateSectionsOrVersions()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        await LegalRequiredContentSeeder.SeedAsync(services);
        await LegalRequiredContentSeeder.SeedAsync(services);

        var sections = await db.LegalSections.Include(s => s.Versions).ToListAsync();
        Assert.Equal(4, sections.Count);
        Assert.All(sections, s => Assert.Single(s.Versions));
    }

    [Fact]
    public async Task SeedAsync_ExistingEmptyUnpublishedDraft_PublishesRealContentInstead()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        // Mirrors what LegalDataSeeder (dev-only) already leaves behind: a section that exists but
        // has never been published, with empty content.
        var termsStub = LegalSection.Create("terms");
        termsStub.UpdateMetadata(
            "terms", "Legal", "pi pi-file-check", 0, null, null, null, null, null, true, true, true);
        termsStub.CreateDraftVersion("Terms", null, string.Empty, null, null);
        db.LegalSections.Add(termsStub);
        await db.SaveChangesAsync();

        await LegalRequiredContentSeeder.SeedAsync(services);

        // SeedAsync ran on a separate DbContext instance (its own scope) — clear this context's
        // change tracker so the read below reflects what was actually persisted, not the stale
        // pre-seed "terms" instance still cached in this context's local identity map.
        db.ChangeTracker.Clear();
        var terms = await db.LegalSections.Include(s => s.Versions).AsNoTracking().SingleAsync(s => s.Slug == "terms");
        Assert.NotNull(terms.PublishedVersionId);
        var published = terms.Versions.Single(v => v.Id == terms.PublishedVersionId);
        Assert.False(string.IsNullOrWhiteSpace(published.ContentEn));
    }

    [Fact]
    public async Task SeedAsync_ExistingPublishedSection_DoesNotOverwriteAdminContent()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        var terms = LegalSection.Create("terms");
        terms.UpdateMetadata(
            "terms", "Legal", "pi pi-file-check", 0, null, null, null, null, null, true, true, true);
        var version = terms.CreateDraftVersion("Admin's Own Terms", null, "Admin-authored content.", null, null);
        terms.PublishVersion(version.Id, "real-admin");
        db.LegalSections.Add(terms);
        await db.SaveChangesAsync();

        await LegalRequiredContentSeeder.SeedAsync(services);

        db.ChangeTracker.Clear();
        var reloaded = await db.LegalSections.Include(s => s.Versions).AsNoTracking().SingleAsync(s => s.Slug == "terms");
        Assert.Single(reloaded.Versions);
        var published = reloaded.Versions.Single();
        Assert.Equal("Admin-authored content.", published.ContentEn);
        Assert.Equal("Admin's Own Terms", published.TitleEn);
    }

    [Fact]
    public async Task SeedAsync_ExistingSectionWithStaleRequireAcceptanceFalse_CorrectsItToTrue()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        // Mirrors the pre-Rev.4 seed default this audit's own history documents: Digital Delivery
        // Policy created with RequireAcceptance=false, unpublished — before that default was fixed.
        var deliveryStub = LegalSection.Create("delivery");
        deliveryStub.UpdateMetadata(
            "delivery", "Commerce", "pi pi-bolt", 0, null, null, null, null, null, true, true, requireAcceptance: false);
        deliveryStub.CreateDraftVersion("DigitalDelivery", null, string.Empty, null, null);
        db.LegalSections.Add(deliveryStub);
        await db.SaveChangesAsync();

        await LegalRequiredContentSeeder.SeedAsync(services);

        db.ChangeTracker.Clear();
        var reloaded = await db.LegalSections.AsNoTracking().SingleAsync(s => s.Slug == "delivery");
        Assert.True(reloaded.RequireAcceptance);
        Assert.NotNull(reloaded.PublishedVersionId);
    }

    [Fact]
    public async Task SeedAsync_ExistingPublishedPlaceholderStub_ReplacesWithRealContentAsNewVersion()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        // Mirrors what was actually found published in the local dev database: a one-word test
        // stub, not real policy text, sitting behind a technically-valid published version.
        var privacyStub = LegalSection.Create("privacy");
        privacyStub.UpdateMetadata(
            "privacy", "Legal", "pi pi-shield", 0, null, null, null, null, null, true, true, true);
        var stubVersion = privacyStub.CreateDraftVersion("Privacy", null, "<p>Privacy</p>", null, null);
        privacyStub.PublishVersion(stubVersion.Id, "qa-tester");
        db.LegalSections.Add(privacyStub);
        await db.SaveChangesAsync();

        await LegalRequiredContentSeeder.SeedAsync(services);

        db.ChangeTracker.Clear();
        var reloaded = await db.LegalSections.Include(s => s.Versions).AsNoTracking().SingleAsync(s => s.Slug == "privacy");

        Assert.Equal(2, reloaded.Versions.Count);
        var published = reloaded.Versions.Single(v => v.Id == reloaded.PublishedVersionId);
        Assert.NotEqual(stubVersion.Id, published.Id);
        Assert.DoesNotContain("<p>Privacy</p>", published.ContentEn, StringComparison.Ordinal);
        Assert.True(published.ContentEn.Length > 100);

        // The stub version itself is preserved in history, just no longer the published one.
        var originalStub = reloaded.Versions.Single(v => v.Id == stubVersion.Id);
        Assert.False(originalStub.IsPublished);
        Assert.Equal("<p>Privacy</p>", originalStub.ContentEn);
    }

    [Fact]
    public async Task SeedAsync_PublishedSections_SatisfyAcceptanceRecorderFilter()
    {
        var (services, db) = BuildProvider($"legal-seed-{Guid.NewGuid():N}");

        await LegalRequiredContentSeeder.SeedAsync(services);

        // Same filter LegalAcceptanceRecorder.RequireAcceptanceSectionsAsync applies — proves the
        // acceptance flow can now actually resolve a version for each required section, not just
        // that rows exist.
        ILegalDbContext legalDb = db;
        var eligible = await legalDb.LegalSections.AsNoTracking()
            .Include(s => s.Versions)
            .Where(s => s.RequireAcceptance && s.PublishedVersionId != null)
            .ToListAsync();

        Assert.Equal(4, eligible.Count);
        foreach (var slug in RequiredSlugs)
        {
            var section = Assert.Single(eligible, s => s.Slug == slug);
            var versionNumber = section.Versions.FirstOrDefault(v => v.Id == section.PublishedVersionId)?.VersionNumber ?? 0;
            Assert.True(versionNumber > 0);
        }
    }
}
