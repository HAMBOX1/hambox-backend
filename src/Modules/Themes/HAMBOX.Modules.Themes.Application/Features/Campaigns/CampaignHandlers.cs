using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Contracts.Campaigns;
using HAMBOX.Modules.Themes.Application.Errors;
using HAMBOX.Modules.Themes.Application.Services;
using HAMBOX.Modules.Themes.Domain.Campaigns;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Themes.Application.Features.Campaigns;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetCampaignsQuery(int PageNumber, int PageSize, string? SearchTerm, string? Status)
    : IRequest<Result<PagedResult<CampaignListItemDto>>>;

internal sealed class GetCampaignsQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetCampaignsQuery, Result<PagedResult<CampaignListItemDto>>>
{
    public async Task<Result<PagedResult<CampaignListItemDto>>> Handle(GetCampaignsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.ThemeCampaigns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c => c.Name.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<CampaignStatus>(request.Status, true, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        // Must match ThemeEngine.ResolveActiveCampaignThemeIdAsync's tiebreak exactly (Priority
        // DESC, StartsAtUtc DESC, CreatedOnUtc ASC) so the admin list can never disagree with the
        // resolver about ordering purely because of a missing final tiebreak. CreatedOnUtc is a
        // DateTimeOffset, which not every provider can translate into an ORDER BY (works fine
        // against SQL Server in every real environment; the SQLite provider used only by the
        // integration test harness can't) — so, same as the resolver itself, the filtered set is
        // fetched once and the tiebreak + pagination both happen in-memory. Campaigns are an
        // admin-configured dataset, not a hot high-volume table.
        var filtered = await query.ToListAsync(cancellationToken);
        var items = filtered
            .OrderByDescending(c => c.Priority)
            .ThenByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var themeIds = items.Select(c => c.ThemeId).Distinct().ToList();
        var themes = await dbContext.StoreThemes.AsNoTracking()
            .Where(t => themeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var now = DateTime.UtcNow;

        // Fetched once, shared by both overlap detection and current-winner computation, so
        // "which campaigns overlap" and "who's actually live" always agree with each other and
        // with ThemeEngine — never two independently-fetched, potentially-divergent snapshots.
        var allLiveCampaigns = await dbContext.ThemeCampaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Published && c.IsEnabled)
            .ToListAsync(cancellationToken);

        var overlappingIds = CampaignOverlapCalculator.FindOverlappingIds(items, allLiveCampaigns);
        var currentWinner = CampaignResolutionOrdering.SelectWinner(
            allLiveCampaigns.Where(c => CampaignResolutionOrdering.IsWithinWindow(c, now)));

        var dtos = items
            .Select(c => CampaignMapper.ToListItem(c, themes.GetValueOrDefault(c.ThemeId), now, overlappingIds.Contains(c.Id), currentWinner))
            .ToList();

        return Result.Success(new PagedResult<CampaignListItemDto>(dtos, request.PageNumber, request.PageSize, total));
    }
}

public sealed record GetCampaignByIdQuery(Guid Id) : IRequest<Result<CampaignDetailDto>>;

internal sealed class GetCampaignByIdQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetCampaignByIdQuery, Result<CampaignDetailDto>>
{
    public async Task<Result<CampaignDetailDto>> Handle(GetCampaignByIdQuery request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignDetailDto>(CampaignErrors.CampaignNotFound);
        }

        var theme = await dbContext.StoreThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == campaign.ThemeId, cancellationToken);

        var now = DateTime.UtcNow;
        var allLiveCampaigns = await dbContext.ThemeCampaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Published && c.IsEnabled)
            .ToListAsync(cancellationToken);

        var overlappingIds = CampaignOverlapCalculator.FindOverlappingIds([campaign], allLiveCampaigns);
        var currentWinner = CampaignResolutionOrdering.SelectWinner(
            allLiveCampaigns.Where(c => CampaignResolutionOrdering.IsWithinWindow(c, now)));

        return Result.Success(CampaignMapper.ToDetail(campaign, theme, now, overlappingIds.Contains(campaign.Id), currentWinner));
    }
}

public sealed record GetCampaignHistoryQuery(Guid CampaignId) : IRequest<Result<IReadOnlyList<CampaignHistoryEntryDto>>>;

internal sealed class GetCampaignHistoryQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetCampaignHistoryQuery, Result<IReadOnlyList<CampaignHistoryEntryDto>>>
{
    public async Task<Result<IReadOnlyList<CampaignHistoryEntryDto>>> Handle(GetCampaignHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await dbContext.CampaignAuditLogs.AsNoTracking()
            .Where(l => l.CampaignId == request.CampaignId)
            .OrderByDescending(l => l.CreatedOnUtc)
            .Select(l => new CampaignHistoryEntryDto(l.Id, l.Action.ToString(), l.ActorUserId, l.CreatedOnUtc.DateTime, l.DetailsJson))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CampaignHistoryEntryDto>>(history);
    }
}

// ─── Commands ──────────────────────────────────────────────────

public sealed record CreateCampaignCommand(CreateCampaignRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateCampaignCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        var themeExists = await dbContext.StoreThemes.AnyAsync(t => t.Id == request.Request.ThemeId, cancellationToken);
        if (!themeExists)
        {
            return Result.Failure<Guid>(CampaignErrors.ThemeNotFound);
        }

        ThemeCampaign campaign;
        try
        {
            campaign = ThemeCampaign.Create(
                request.Request.Name,
                request.Request.Description,
                request.Request.ThemeId,
                request.Request.StartsAtUtc,
                request.Request.EndsAtUtc,
                request.Request.Priority);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(new Error("Campaign.InvalidRequest", ex.Message));
        }

        dbContext.ThemeCampaigns.Add(campaign);
        CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Created, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(campaign.Id);
    }
}

public sealed record UpdateCampaignCommand(Guid Id, UpdateCampaignRequest Request) : IRequest<Result>;

internal sealed class UpdateCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateCampaignCommand, Result>
{
    public async Task<Result> Handle(UpdateCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(CampaignErrors.CampaignNotFound);
        }

        var themeExists = await dbContext.StoreThemes.AnyAsync(t => t.Id == request.Request.ThemeId, cancellationToken);
        if (!themeExists)
        {
            return Result.Failure(CampaignErrors.ThemeNotFound);
        }

        // Campaigns remain editable in every status (including Active) — this is operational
        // config, not a versioned content artifact like a theme's token payload, so there's no
        // immutability guard here. Every field group that actually changed gets its own audit
        // action, matching the granularity ThemeAuditAction already uses for themes.
        var themeChanged = campaign.ThemeId != request.Request.ThemeId;
        var scheduleChanged = campaign.StartsAtUtc != request.Request.StartsAtUtc
            || campaign.EndsAtUtc != request.Request.EndsAtUtc
            || campaign.Priority != request.Request.Priority;
        var detailsChanged = campaign.Name != request.Request.Name.Trim()
            || campaign.Description != request.Request.Description?.Trim();

        try
        {
            campaign.UpdateDetails(request.Request.Name, request.Request.Description);
            campaign.Reschedule(request.Request.StartsAtUtc, request.Request.EndsAtUtc, request.Request.Priority);
            campaign.ChangeTheme(request.Request.ThemeId);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(new Error("Campaign.InvalidRequest", ex.Message));
        }

        if (detailsChanged)
        {
            CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Updated, currentUser.UserId);
        }

        if (themeChanged)
        {
            CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.ThemeChanged, currentUser.UserId, $"{{\"themeId\":\"{request.Request.ThemeId}\"}}");
        }

        if (scheduleChanged)
        {
            CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.ScheduleChanged, currentUser.UserId,
                $"{{\"startsAtUtc\":\"{request.Request.StartsAtUtc:O}\",\"endsAtUtc\":\"{request.Request.EndsAtUtc:O}\",\"priority\":{request.Request.Priority}}}");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(CampaignErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}

public sealed record PublishCampaignCommand(Guid Id) : IRequest<Result>;

internal sealed class PublishCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<PublishCampaignCommand, Result>
{
    public async Task<Result> Handle(PublishCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(CampaignErrors.CampaignNotFound);
        }

        if (campaign.Status == CampaignStatus.Archived)
        {
            return Result.Failure(CampaignErrors.NotDraft);
        }

        if (campaign.Status == CampaignStatus.Published)
        {
            return Result.Failure(CampaignErrors.AlreadyPublished);
        }

        // The owner-safety rule this feature exists for: a campaign can be configured against a
        // Draft theme while being built, but it must not go live pointing at one. This aggregate
        // has no visibility into StoreTheme's state, so the check happens here, in the handler,
        // against a freshly-loaded theme — not inside ThemeCampaign.Publish().
        var theme = await dbContext.StoreThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == campaign.ThemeId, cancellationToken);
        if (!CampaignMapper.IsThemePublishable(theme))
        {
            return Result.Failure(CampaignErrors.ThemeNotPublishable);
        }

        campaign.Publish();
        CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Published, currentUser.UserId);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(CampaignErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}

public sealed record EnableCampaignCommand(Guid Id) : IRequest<Result>;

internal sealed class EnableCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<EnableCampaignCommand, Result>
{
    public async Task<Result> Handle(EnableCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(CampaignErrors.CampaignNotFound);
        }

        campaign.Enable();
        CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Enabled, currentUser.UserId);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(CampaignErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}

public sealed record DisableCampaignCommand(Guid Id) : IRequest<Result>;

internal sealed class DisableCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DisableCampaignCommand, Result>
{
    public async Task<Result> Handle(DisableCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(CampaignErrors.CampaignNotFound);
        }

        campaign.Disable();
        CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Disabled, currentUser.UserId);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(CampaignErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}

public sealed record ArchiveCampaignCommand(Guid Id) : IRequest<Result>;

internal sealed class ArchiveCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ArchiveCampaignCommand, Result>
{
    public async Task<Result> Handle(ArchiveCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(CampaignErrors.CampaignNotFound);
        }

        campaign.Archive();
        CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Archived, currentUser.UserId);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(CampaignErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}

public sealed record DeleteCampaignCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteCampaignCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteCampaignCommand, Result>
{
    public async Task<Result> Handle(DeleteCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.ThemeCampaigns.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(CampaignErrors.CampaignNotFound);
        }

        // Mirrors the "safely stop" workflow: a live campaign a customer might currently be
        // seeing can't be deleted out from under them by accident — disable or archive it first.
        if (campaign.Status == CampaignStatus.Published && campaign.IsEnabled)
        {
            return Result.Failure(CampaignErrors.CannotDeleteWhileLive);
        }

        campaign.IsDeleted = true;
        campaign.DeletedOnUtc = DateTimeOffset.UtcNow;
        CampaignAuditWriter.Record(dbContext, campaign.Id, CampaignAuditAction.Deleted, currentUser.UserId);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(CampaignErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}
