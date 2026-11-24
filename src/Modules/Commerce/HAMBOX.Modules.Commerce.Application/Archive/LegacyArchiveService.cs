using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Archive;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Archive;

public sealed record ArchiveCustomerRow(int Row, string? LegacyId, string? Email, string? Phone, string? Name, DateTimeOffset? RegisteredOnUtc, int PromisedStars);

public sealed record ArchiveOrderRow(int Row, string? LegacyOrderId, string? LegacyCustomerId, decimal Total, string? Currency, string? Status, DateTimeOffset? OrderedOnUtc);

public sealed record ParsedArchive(IReadOnlyList<ArchiveCustomerRow> Customers, IReadOnlyList<ArchiveOrderRow> Orders);

/// <summary>Reads an uploaded CSV/XLSX archive (implemented in Infrastructure, which owns the spreadsheet library).</summary>
public interface ILegacyArchiveParser
{
    ParsedArchive Parse(Stream content, string fileName);
}

public sealed record ArchiveIssue(string Sheet, int Row, string Key, string Reason);

public sealed record ArchiveImportReport(
    bool Committed, int NewCustomers, int AlreadyImported, int NewOrders, int OrdersAlreadyImported, int TotalPromisedStars,
    IReadOnlyList<ArchiveIssue> Issues);

public sealed record ArchiveClaimResult(int Linked, int StarsCredited);

/// <summary>
/// Brings the old archive in safely (CU-03): a dry-run preview with a reconciliation report before anything is written, a
/// repeatable import keyed on the legacy id (so running it twice changes nothing), and linking to real accounts only after
/// ownership is established by a verified e-mail or by a staff decision with a reason.
/// </summary>
public sealed class LegacyArchiveService(ICommerceDbContext db, IUserDirectory users, StarsLedgerService stars)
{
    public async Task<ArchiveImportReport> ImportAsync(ParsedArchive archive, bool commit, CancellationToken cancellationToken)
    {
        var issues = new List<ArchiveIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = archive.Customers.Where(c => !string.IsNullOrWhiteSpace(c.LegacyId)).Select(c => c.LegacyId!.Trim()).Distinct().ToList();
        var existing = (await db.LegacyCustomerRecords.AsNoTracking().Where(r => ids.Contains(r.LegacyId)).Select(r => r.LegacyId).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int created = 0, already = 0, promised = 0;
        var emailsSeen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in archive.Customers)
        {
            if (string.IsNullOrWhiteSpace(row.LegacyId))
            {
                issues.Add(new ArchiveIssue("Customers", row.Row, string.Empty, "Missing customer id."));
                continue;
            }

            var key = row.LegacyId.Trim();
            if (!seen.Add(key))
            {
                issues.Add(new ArchiveIssue("Customers", row.Row, key, "Duplicate customer id in the file."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Email) && !row.Email.Contains('@'))
            {
                issues.Add(new ArchiveIssue("Customers", row.Row, key, "The e-mail address is not valid."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Email))
            {
                if (emailsSeen.TryGetValue(row.Email.Trim(), out var other) && other != key)
                {
                    issues.Add(new ArchiveIssue("Customers", row.Row, key, $"Same e-mail as customer {other} — review before linking."));
                }
                else
                {
                    emailsSeen[row.Email.Trim()] = key;
                }
            }

            if (existing.Contains(key))
            {
                already++;
                continue;
            }

            created++;
            promised += Math.Max(row.PromisedStars, 0);
            if (commit)
            {
                db.LegacyCustomerRecords.Add(LegacyCustomerRecord.Create(key, row.Email, row.Phone, row.Name, row.RegisteredOnUtc, row.PromisedStars));
            }
        }

        var orderIds = archive.Orders.Where(o => !string.IsNullOrWhiteSpace(o.LegacyOrderId)).Select(o => o.LegacyOrderId!.Trim()).Distinct().ToList();
        var existingOrders = (await db.LegacyOrderRecords.AsNoTracking().Where(o => orderIds.Contains(o.LegacyOrderId)).Select(o => o.LegacyOrderId).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var known = seen.Concat(await db.LegacyCustomerRecords.AsNoTracking().Select(r => r.LegacyId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenOrders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int newOrders = 0, ordersAlready = 0;
        foreach (var row in archive.Orders)
        {
            if (string.IsNullOrWhiteSpace(row.LegacyOrderId) || string.IsNullOrWhiteSpace(row.LegacyCustomerId))
            {
                issues.Add(new ArchiveIssue("Orders", row.Row, row.LegacyOrderId ?? string.Empty, "Missing order id or customer id."));
                continue;
            }

            var key = row.LegacyOrderId.Trim();
            if (!seenOrders.Add(key))
            {
                issues.Add(new ArchiveIssue("Orders", row.Row, key, "Duplicate order id in the file."));
                continue;
            }

            if (!known.Contains(row.LegacyCustomerId.Trim()))
            {
                issues.Add(new ArchiveIssue("Orders", row.Row, key, $"Customer {row.LegacyCustomerId} is not in the archive."));
                continue;
            }

            if (existingOrders.Contains(key))
            {
                ordersAlready++;
                continue;
            }

            newOrders++;
            if (commit)
            {
                db.LegacyOrderRecords.Add(LegacyOrderRecord.Create(key, row.LegacyCustomerId, row.Total, row.Currency, row.Status, row.OrderedOnUtc));
            }
        }

        if (commit)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ArchiveImportReport(commit, created, already, newOrders, ordersAlready, promised, issues);
    }

    /// <summary>Links archive records whose e-mail matches the customer's <em>verified</em> e-mail, and credits any documented promised Stars once.</summary>
    public async Task<ArchiveClaimResult> ClaimForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var email = await users.GetVerifiedEmailAsync(userId, cancellationToken);
        if (email is null)
        {
            return new ArchiveClaimResult(0, 0);
        }

        var normalized = email.Trim().ToLowerInvariant();
        var matches = await db.LegacyCustomerRecords
            .Where(r => r.Status == LegacyLinkStatus.Unlinked && r.Email == normalized)
            .ToListAsync(cancellationToken);

        return await LinkAsync(matches, userId, "system", "Verified e-mail matches the archive record.", cancellationToken);
    }

    /// <summary>Staff decision to link one record to an account, with the reason on file.</summary>
    public async Task<ArchiveClaimResult> LinkManuallyAsync(Guid recordId, string userId, string actorUserId, string reason, CancellationToken cancellationToken)
    {
        var record = await db.LegacyCustomerRecords.Where(r => r.Id == recordId && r.Status == LegacyLinkStatus.Unlinked).ToListAsync(cancellationToken);
        return await LinkAsync(record, userId, actorUserId, reason, cancellationToken);
    }

    private async Task<ArchiveClaimResult> LinkAsync(
        IReadOnlyList<LegacyCustomerRecord> records, string userId, string actor, string reason, CancellationToken cancellationToken)
    {
        var linked = 0;
        var credited = 0;
        foreach (var record in records)
        {
            if (!record.Link(userId, actor, reason))
            {
                continue;
            }

            linked++;
            await db.SaveChangesAsync(cancellationToken);

            if (record.PromisedStars > 0
                && await stars.CreditAsync(
                    userId, PointsTransactionType.Bonus, record.PromisedStars, "Stars carried over from the previous HAMBOX programme.",
                    $"legacy:{record.LegacyId}", reference: record.LegacyId, cancellationToken: cancellationToken))
            {
                credited += record.PromisedStars;
            }
        }

        return new ArchiveClaimResult(linked, credited);
    }
}
