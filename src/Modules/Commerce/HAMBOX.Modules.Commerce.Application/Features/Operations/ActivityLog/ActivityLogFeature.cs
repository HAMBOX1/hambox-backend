using System.Globalization;
using System.Text;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Operations.ActivityLog;

/// <summary>
/// One row of the unified, searchable activity log (audit R56): the audit trails that exist per area (orders,
/// referrals, memberships, promotions, operations) read as a single stream with one field set.
/// </summary>
public sealed record ActivityLogEntryDto(
    Guid Id,
    string Source,
    string EventType,
    string? Description,
    string? ActorUserId,
    string? ActorName,
    string? EntityType,
    string? EntityId,
    string? Reason,
    string? OldValue,
    string? NewValue,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredOnUtc);

/// <summary>
/// <paramref name="Source"/>: Order | Referral | Membership | Promotion | Operations (null = all).
/// <paramref name="EntityId"/>: restricts to one entity (e.g. an order id) — what "export this order's log" uses.
/// </summary>
public sealed record GetActivityLogQuery(
    string? Search,
    string? Source,
    string? EntityId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize,
    int MaxPageSize = 200) : IRequest<Result<PagedResult<ActivityLogEntryDto>>>;

internal sealed class GetActivityLogQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetActivityLogQuery, Result<PagedResult<ActivityLogEntryDto>>>
{
    /// <summary>Per-source cap before paging: bounds the work while the newest rows are always complete.</summary>
    private const int PerSourceCap = 2000;

    public async Task<Result<PagedResult<ActivityLogEntryDto>>> Handle(
        GetActivityLogQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, request.MaxPageSize);
        var search = request.Search?.Trim();
        var source = request.Source?.Trim();
        bool Wants(string name) => string.IsNullOrEmpty(source) || source.Equals(name, StringComparison.OrdinalIgnoreCase);

        var rows = new List<ActivityLogEntryDto>();

        if (Wants("Order"))
        {
            var q = db.OrderAuditEntries.AsNoTracking().AsQueryable();
            if (Guid.TryParse(request.EntityId, out var orderId))
            {
                q = q.Where(e => e.OrderId == orderId);
            }
            else if (!string.IsNullOrEmpty(request.EntityId))
            {
                q = q.Where(_ => false);
            }

            if (request.From is { } from) q = q.Where(e => e.OccurredOnUtc >= from);
            if (request.To is { } to) q = q.Where(e => e.OccurredOnUtc <= to);
            if (!string.IsNullOrEmpty(search))
            {
                q = q.Where(e =>
                    e.EventType.Contains(search) || e.Description.Contains(search)
                    || (e.ActorDisplayName != null && e.ActorDisplayName.Contains(search))
                    || (e.Reason != null && e.Reason.Contains(search))
                    || (e.IpAddress != null && e.IpAddress.Contains(search)));
            }

            var list = await q.OrderByDescending(e => e.OccurredOnUtc).Take(PerSourceCap).ToListAsync(cancellationToken);
            rows.AddRange(list.Select(e => new ActivityLogEntryDto(
                e.Id, "Order", e.EventType, e.Description, e.ActorUserId, e.ActorDisplayName, "Order",
                e.OrderId.ToString(), e.Reason, e.OldValue, e.NewValue, e.IpAddress, e.UserAgent, e.OccurredOnUtc)));
        }

        if (Wants("Operations"))
        {
            var q = db.OperationalAuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(request.EntityId)) q = q.Where(e => e.EntityId == request.EntityId);
            if (request.From is { } from) q = q.Where(e => e.OccurredOnUtc >= from);
            if (request.To is { } to) q = q.Where(e => e.OccurredOnUtc <= to);
            if (!string.IsNullOrEmpty(search))
            {
                q = q.Where(e =>
                    e.Action.Contains(search) || (e.Details != null && e.Details.Contains(search))
                    || (e.ActorDisplayName != null && e.ActorDisplayName.Contains(search))
                    || (e.EntityId != null && e.EntityId.Contains(search)));
            }

            var list = await q.OrderByDescending(e => e.OccurredOnUtc).Take(PerSourceCap).ToListAsync(cancellationToken);
            rows.AddRange(list.Select(e => new ActivityLogEntryDto(
                e.Id, "Operations", e.Action, e.Details, e.ActorUserId, e.ActorDisplayName, e.EntityType,
                e.EntityId, null, null, null, null, null, e.OccurredOnUtc)));
        }

        // The enum-typed audit trails: filter by date / entity in the database, by event name in memory.
        var from2 = request.From?.UtcDateTime;
        var to2 = request.To?.UtcDateTime;

        if (Wants("Referral"))
        {
            var q = db.ReferralAuditLogs.AsNoTracking().AsQueryable();
            if (Guid.TryParse(request.EntityId, out var id)) q = q.Where(e => e.ReferralHistoryEntryId == id);
            else if (!string.IsNullOrEmpty(request.EntityId)) q = q.Where(_ => false);
            if (from2 is { } f) q = q.Where(e => e.OccurredOnUtc >= f);
            if (to2 is { } t) q = q.Where(e => e.OccurredOnUtc <= t);

            var list = await q.OrderByDescending(e => e.OccurredOnUtc).Take(PerSourceCap).ToListAsync(cancellationToken);
            rows.AddRange(list.Select(e => new ActivityLogEntryDto(
                e.Id, "Referral", e.Action.ToString(),
                e.Points is { } p ? $"{e.Details} ({p:+#;-#;0} points)" : e.Details,
                e.PerformedByUserId, null, "Referral", e.ReferralHistoryEntryId.ToString(),
                null, null, null, null, null, AsUtc(e.OccurredOnUtc))));
        }

        if (Wants("Membership"))
        {
            var q = db.MembershipAuditLogs.AsNoTracking().AsQueryable();
            if (Guid.TryParse(request.EntityId, out var id)) q = q.Where(e => e.SubscriptionId == id || e.PlanId == id);
            else if (!string.IsNullOrEmpty(request.EntityId)) q = q.Where(_ => false);
            if (from2 is { } f) q = q.Where(e => e.OccurredOnUtc >= f);
            if (to2 is { } t) q = q.Where(e => e.OccurredOnUtc <= t);

            var list = await q.OrderByDescending(e => e.OccurredOnUtc).Take(PerSourceCap).ToListAsync(cancellationToken);
            rows.AddRange(list.Select(e => new ActivityLogEntryDto(
                e.Id, "Membership", e.Action.ToString(), e.Details, e.PerformedByUserId, null, "Membership",
                (e.SubscriptionId ?? e.PlanId)?.ToString(), null, null, null, null, null, AsUtc(e.OccurredOnUtc))));
        }

        if (Wants("Promotion"))
        {
            var q = db.PromotionAuditLogs.AsNoTracking().AsQueryable();
            if (Guid.TryParse(request.EntityId, out var id)) q = q.Where(e => e.PromotionId == id || e.CouponCodeId == id);
            else if (!string.IsNullOrEmpty(request.EntityId)) q = q.Where(_ => false);
            if (from2 is { } f) q = q.Where(e => e.OccurredOnUtc >= f);
            if (to2 is { } t) q = q.Where(e => e.OccurredOnUtc <= t);

            var list = await q.OrderByDescending(e => e.OccurredOnUtc).Take(PerSourceCap).ToListAsync(cancellationToken);
            rows.AddRange(list.Select(e => new ActivityLogEntryDto(
                e.Id, "Promotion", e.Action.ToString(), e.Details, e.PerformedByUserId, null, "Promotion",
                (e.PromotionId ?? e.CouponCodeId)?.ToString(), null, null, null, null, null, AsUtc(e.OccurredOnUtc))));
        }

        if (!string.IsNullOrEmpty(search))
        {
            rows = rows.Where(r => Matches(r, search)).ToList();
        }

        var ordered = rows.OrderByDescending(r => r.OccurredOnUtc).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result.Success(new PagedResult<ActivityLogEntryDto>(items, page, pageSize, ordered.Count));
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static bool Matches(ActivityLogEntryDto r, string term) =>
        Contains(r.EventType, term) || Contains(r.Description, term) || Contains(r.ActorName, term)
        || Contains(r.ActorUserId, term) || Contains(r.EntityId, term) || Contains(r.Reason, term)
        || Contains(r.IpAddress, term);

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}

/// <summary>CSV for the log (Excel-friendly: UTF-8 with BOM, formula-injection neutralised, RFC 4180 quoting).</summary>
public static class ActivityLogCsv
{
    private static readonly string[] Header =
    [
        "Time (UTC)", "Source", "Event", "Description", "Actor", "Actor id", "Entity", "Entity id",
        "Reason", "Old value", "New value", "IP address", "User agent",
    ];

    public static string Build(IEnumerable<ActivityLogEntryDto> rows)
    {
        var builder = new StringBuilder();
        builder.Append('﻿');
        builder.AppendLine(string.Join(",", Header.Select(Escape)));

        foreach (var r in rows)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                r.OccurredOnUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                r.Source, r.EventType, r.Description, r.ActorName, r.ActorUserId, r.EntityType, r.EntityId,
                r.Reason, r.OldValue, r.NewValue, r.IpAddress, r.UserAgent,
            }.Select(Escape)));
        }

        return builder.ToString();
    }

    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // A cell that starts with = + - @ would be run as a formula by a spreadsheet; neutralise it.
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

/// <summary>Records that a visitor opened the pre-filled WhatsApp support chat (audit R51).</summary>
public sealed record LogSupportContactClickCommand(
    string Channel,
    string? Context,
    string? OrderId,
    string? ProductId,
    string? Page) : IRequest<Result>;

internal sealed class LogSupportContactClickCommandHandler(
    ICommerceDbContext db,
    HAMBOX.Application.Abstractions.ICurrentUserService currentUser)
    : IRequestHandler<LogSupportContactClickCommand, Result>
{
    private static readonly string[] AllowedChannels = ["whatsapp"];

    public async Task<Result> Handle(LogSupportContactClickCommand request, CancellationToken cancellationToken)
    {
        var channel = request.Channel.Trim().ToLowerInvariant();
        if (!AllowedChannels.Contains(channel))
        {
            return Result.Failure(HAMBOX.Modules.Commerce.Application.Errors.CommerceErrors.SupportChannelInvalid);
        }

        var details = string.Join(
            "; ",
            new[]
            {
                $"channel={channel}",
                request.Context is { Length: > 0 } c ? $"context={Trim(c, 40)}" : null,
                request.Page is { Length: > 0 } p ? $"page={Trim(p, 120)}" : null,
                currentUser.IpAddress is { Length: > 0 } ip ? $"ip={Trim(ip, 64)}" : null,
            }.Where(x => x is not null));

        var (entityType, entityId) = request.OrderId is { Length: > 0 } o
            ? ("Order", Trim(o, 64))
            : request.ProductId is { Length: > 0 } pr ? ("Product", Trim(pr, 64)) : ((string?)null, (string?)null);

        db.OperationalAuditLogs.Add(HAMBOX.Modules.Commerce.Domain.Operations.OperationalAuditLog.Create(
            "SupportWhatsAppClicked",
            currentUser.UserId,
            currentUser.DisplayName,
            entityType,
            entityId,
            details));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];
}
