using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Domain.Legal;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Legal.Application.Services;

/// <summary>
/// Records and checks user acceptance of legal sections flagged <see cref="LegalSection.RequireAcceptance"/>.
/// Normalized to one row per (user, section) acceptance event so it scales to any number of sections,
/// unlike the fixed Terms/Privacy/Refund columns this replaces. The server always uses the currently
/// published version number for each section — never a client-supplied value.
/// </summary>
public static class LegalAcceptanceRecorder
{
    public static async Task RecordAsync(
        ILegalDbContext dbContext,
        string userId,
        string ipAddress,
        string userAgent,
        string language,
        CancellationToken cancellationToken = default) =>
        await RecordAsync(dbContext, userId, ipAddress, userAgent, language, orderId: null, cancellationToken);

    /// <summary>
    /// Same as the registration-time overload, but ties the acceptance rows to the order they
    /// gated — required at checkout (contract §33.1: User ID, Order ID, Policy Version, Timestamp,
    /// IP, Device, captured before payment). Call this right after the order is created and before
    /// the payment provider is invoked, in the same handler.
    /// </summary>
    public static async Task RecordAsync(
        ILegalDbContext dbContext,
        string userId,
        string ipAddress,
        string userAgent,
        string language,
        Guid? orderId,
        CancellationToken cancellationToken = default)
    {
        var sections = await RequireAcceptanceSectionsAsync(dbContext, cancellationToken);

        foreach (var (section, publishedVersionNumber) in sections)
        {
            dbContext.LegalSectionAcceptances.Add(
                LegalSectionAcceptance.Create(
                    userId, section.Id, publishedVersionNumber, ipAddress, userAgent, language, orderId));
        }
    }

    public static async Task<IReadOnlyList<string>> GetStaleSlugsAsync(
        ILegalDbContext dbContext, string userId, CancellationToken cancellationToken = default)
    {
        var sections = await RequireAcceptanceSectionsAsync(dbContext, cancellationToken);
        if (sections.Count == 0)
        {
            return [];
        }

        var sectionIds = sections.Select(s => s.Section.Id).ToList();

        var latestAcceptedVersions = await dbContext.LegalSectionAcceptances.AsNoTracking()
            .Where(a => a.UserId == userId && sectionIds.Contains(a.LegalSectionId))
            .GroupBy(a => a.LegalSectionId)
            .Select(g => new { LegalSectionId = g.Key, MaxVersion = g.Max(a => a.VersionNumber) })
            .ToDictionaryAsync(x => x.LegalSectionId, x => x.MaxVersion, cancellationToken);

        var stale = new List<string>();
        foreach (var (section, publishedVersionNumber) in sections)
        {
            var acceptedVersion = latestAcceptedVersions.GetValueOrDefault(section.Id, 0);
            if (acceptedVersion < publishedVersionNumber)
            {
                stale.Add(section.Slug);
            }
        }

        return stale;
    }

    private static async Task<List<(LegalSection Section, int VersionNumber)>> RequireAcceptanceSectionsAsync(
        ILegalDbContext dbContext, CancellationToken cancellationToken)
    {
        var sections = await dbContext.LegalSections.AsNoTracking()
            .Include(s => s.Versions)
            .Where(s => s.RequireAcceptance && s.PublishedVersionId != null)
            .ToListAsync(cancellationToken);

        return sections
            .Select(s => (Section: s, VersionNumber: s.Versions.FirstOrDefault(v => v.Id == s.PublishedVersionId)?.VersionNumber ?? 0))
            .Where(x => x.VersionNumber > 0)
            .ToList();
    }
}
