using HAMBOX.SharedKernel.Errors;
using HAMBOX.Modules.Themes.Application.Errors;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Contracts.Themes;
using HAMBOX.Modules.Themes.Application.Services;
using HAMBOX.Modules.Themes.Domain.Themes;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Themes.Application.Features.Themes;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetThemesQuery(int PageNumber, int PageSize, string? SearchTerm, string? Status)
    : IRequest<Result<PagedResult<ThemeListItemDto>>>;

internal sealed class GetThemesQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetThemesQuery, Result<PagedResult<ThemeListItemDto>>>
{
    public async Task<Result<PagedResult<ThemeListItemDto>>> Handle(GetThemesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.StoreThemes.AsNoTracking().Where(t => !t.IsDeleted && !t.IsTemplate);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(t => t.Name.Contains(term) || t.Slug.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<StoreThemeStatus>(request.Status, true, out var status))
        {
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var itemIds = items.Select(i => i.Id).ToList();
        var versions = await dbContext.ThemeVersions
            .AsNoTracking()
            .Where(v => itemIds.Contains(v.ThemeId))
            .ToListAsync(cancellationToken);

        var versionsByTheme = versions.GroupBy(v => v.ThemeId).ToDictionary(g => g.Key, g => g.ToList());

        var assignments = await dbContext.ThemeAssignments
            .AsNoTracking()
            .Where(a => itemIds.Contains(a.ThemeId) && a.IsActive)
            .ToListAsync(cancellationToken);
        var assignmentsByTheme = assignments.GroupBy(a => a.ThemeId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = items.Select(t =>
        {
            var themeVersions = versionsByTheme.GetValueOrDefault(t.Id, []);
            var publishedOn = themeVersions.Where(v => v.IsPublished).Select(v => v.PublishedOnUtc).Max();
            var previewVersion = themeVersions.FirstOrDefault(v => v.IsPublished)
                ?? themeVersions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            var previewTokens = previewVersion?.GetTokens();

            var topAssignment = assignmentsByTheme.GetValueOrDefault(t.Id, [])
                .OrderByDescending(a => a.Priority)
                .FirstOrDefault();
            var assignmentSummary = topAssignment is null ? null : $"{topAssignment.AssignmentType}: {topAssignment.TargetKey}";

            return ThemeMapper.ToListItem(t, publishedOn, themeVersions.Count, previewTokens, assignmentSummary);
        }).ToList();

        return Result.Success(new PagedResult<ThemeListItemDto>(dtos, request.PageNumber, request.PageSize, total));
    }
}

public sealed record GetThemeTemplatesQuery() : IRequest<Result<IReadOnlyList<ThemeTemplateDto>>>;

internal sealed class GetThemeTemplatesQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetThemeTemplatesQuery, Result<IReadOnlyList<ThemeTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<ThemeTemplateDto>>> Handle(GetThemeTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await dbContext.StoreThemes.AsNoTracking()
            .Where(t => !t.IsDeleted && t.IsTemplate)
            .Include(t => t.Versions)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var dtos = templates
            .Select(t =>
            {
                var version = t.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                return new ThemeTemplateDto(t.Id, t.Name, t.Description, t.BaseMode.ToString(), version?.GetTokens() ?? new Dictionary<string, string>());
            })
            .ToList();

        return Result.Success<IReadOnlyList<ThemeTemplateDto>>(dtos);
    }
}

public sealed record GetThemeByIdQuery(Guid Id) : IRequest<Result<ThemeDetailDto>>;

internal sealed class GetThemeByIdQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetThemeByIdQuery, Result<ThemeDetailDto>>
{
    public async Task<Result<ThemeDetailDto>> Handle(GetThemeByIdQuery request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (theme is null)
        {
            return Result.Failure<ThemeDetailDto>(ThemeErrors.ThemeNotFound);
        }

        var versions = await dbContext.ThemeVersions.AsNoTracking()
            .Where(v => v.ThemeId == request.Id).OrderByDescending(v => v.VersionNumber).ToListAsync(cancellationToken);
        var assets = await dbContext.ThemeAssets.AsNoTracking().Where(a => a.ThemeId == request.Id).ToListAsync(cancellationToken);
        var schedules = await dbContext.ThemeSchedules.AsNoTracking().Where(s => s.ThemeId == request.Id).ToListAsync(cancellationToken);
        var assignments = await dbContext.ThemeAssignments.AsNoTracking().Where(a => a.ThemeId == request.Id).ToListAsync(cancellationToken);

        return Result.Success(ThemeMapper.ToDetail(theme, versions, assets, schedules, assignments));
    }
}

public sealed record GetActiveThemeQuery(string? MembershipPlanSlug)
    : IRequest<Result<ActiveThemeDto>>;

internal sealed class GetActiveThemeQueryHandler(IThemeEngine themeEngine, ICurrentUserService currentUser)
    : IRequestHandler<GetActiveThemeQuery, Result<ActiveThemeDto>>
{
    public async Task<Result<ActiveThemeDto>> Handle(GetActiveThemeQuery request, CancellationToken cancellationToken)
    {
        var active = await themeEngine.ResolveActiveThemeAsync(
            currentUser.UserId,
            request.MembershipPlanSlug,
            cancellationToken: cancellationToken);

        return active is null
            ? Result.Failure<ActiveThemeDto>(ThemeErrors.ThemeNoActive)
            : Result.Success(active);
    }
}

public sealed record GetPreviewThemeQuery(string Token) : IRequest<Result<ActiveThemeDto>>;

internal sealed class GetPreviewThemeQueryHandler(IThemeEngine themeEngine)
    : IRequestHandler<GetPreviewThemeQuery, Result<ActiveThemeDto>>
{
    public async Task<Result<ActiveThemeDto>> Handle(GetPreviewThemeQuery request, CancellationToken cancellationToken)
    {
        var preview = await themeEngine.ResolvePreviewAsync(request.Token, cancellationToken);
        return preview is null
            ? Result.Failure<ActiveThemeDto>(ThemeErrors.ThemePreviewExpired)
            : Result.Success(preview);
    }
}

public sealed record GetThemeStatisticsQuery() : IRequest<Result<ThemeStatisticsDto>>;

internal sealed class GetThemeStatisticsQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetThemeStatisticsQuery, Result<ThemeStatisticsDto>>
{
    public async Task<Result<ThemeStatisticsDto>> Handle(GetThemeStatisticsQuery request, CancellationToken cancellationToken)
    {
        var themes = await dbContext.StoreThemes.AsNoTracking().Where(t => !t.IsDeleted).ToListAsync(cancellationToken);
        var schedules = await dbContext.ThemeSchedules.AsNoTracking().CountAsync(s => s.IsActive, cancellationToken);
        var assignments = await dbContext.ThemeAssignments.AsNoTracking().CountAsync(a => a.IsActive, cancellationToken);

        return Result.Success(new ThemeStatisticsDto(
            themes.Count,
            themes.Count(t => t.Status == StoreThemeStatus.Published),
            themes.Count(t => t.Status == StoreThemeStatus.Draft),
            themes.Count(t => t.Status == StoreThemeStatus.Archived),
            schedules,
            assignments));
    }
}

public sealed record GetThemeHistoryQuery(Guid ThemeId) : IRequest<Result<IReadOnlyList<ThemeHistoryEntryDto>>>;

internal sealed class GetThemeHistoryQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<GetThemeHistoryQuery, Result<IReadOnlyList<ThemeHistoryEntryDto>>>
{
    public async Task<Result<IReadOnlyList<ThemeHistoryEntryDto>>> Handle(GetThemeHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await dbContext.ThemeAuditLogs.AsNoTracking()
            .Where(l => l.ThemeId == request.ThemeId)
            .OrderByDescending(l => l.CreatedOnUtc)
            .Select(l => new ThemeHistoryEntryDto(l.Id, l.Action.ToString(), l.ActorUserId, l.CreatedOnUtc.DateTime, l.DetailsJson))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ThemeHistoryEntryDto>>(history);
    }
}

public sealed record CompareThemesQuery(Guid LeftThemeId, Guid RightThemeId) : IRequest<Result<ThemeCompareDto>>;

internal sealed class CompareThemesQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<CompareThemesQuery, Result<ThemeCompareDto>>
{
    public async Task<Result<ThemeCompareDto>> Handle(CompareThemesQuery request, CancellationToken cancellationToken)
    {
        async Task<Dictionary<string, string>?> LoadTokens(Guid themeId)
        {
            var theme = await dbContext.StoreThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == themeId, cancellationToken);
            if (theme?.PublishedVersionId is not Guid versionId)
            {
                return null;
            }

            var version = await dbContext.ThemeVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
            return version?.GetTokens();
        }

        var left = await LoadTokens(request.LeftThemeId);
        var right = await LoadTokens(request.RightThemeId);
        if (left is null || right is null)
        {
            return Result.Failure<ThemeCompareDto>(ThemeErrors.ThemeCompareFailed);
        }

        var keys = left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase);
        var diffs = keys
            .Where(k => !string.Equals(left.GetValueOrDefault(k), right.GetValueOrDefault(k), StringComparison.OrdinalIgnoreCase))
            .Select(k => new ThemeTokenDiffDto(k, left.GetValueOrDefault(k), right.GetValueOrDefault(k)))
            .ToList();

        return Result.Success(new ThemeCompareDto(request.LeftThemeId, request.RightThemeId, diffs));
    }
}

public sealed record ExportThemeQuery(Guid Id) : IRequest<Result<ExportThemeDto>>;

internal sealed class ExportThemeQueryHandler(IThemesDbContext dbContext)
    : IRequestHandler<ExportThemeQuery, Result<ExportThemeDto>>
{
    public async Task<Result<ExportThemeDto>> Handle(ExportThemeQuery request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure<ExportThemeDto>(ThemeErrors.ThemeNotFound);
        }

        var version = theme.PublishedVersionId.HasValue
            ? await dbContext.ThemeVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == theme.PublishedVersionId, cancellationToken)
            : await dbContext.ThemeVersions.AsNoTracking().Where(v => v.ThemeId == theme.Id).OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            return Result.Failure<ExportThemeDto>(ThemeErrors.ThemeNoVersion);
        }

        var assets = await dbContext.ThemeAssets.AsNoTracking()
            .Where(a => a.ThemeId == theme.Id)
            .Select(a => new ThemeAssetDto(a.AssetType.ToString(), a.Url, a.AltText))
            .ToListAsync(cancellationToken);

        return Result.Success(new ExportThemeDto(
            theme.Name,
            theme.Slug,
            theme.BaseMode.ToString(),
            version.GetTokens(),
            assets,
            version.VersionNumber));
    }
}

// ─── Commands ──────────────────────────────────────────────────

public sealed record CreateThemeCommand(CreateThemeRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateThemeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateThemeCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ThemeBaseMode>(request.Request.BaseMode, true, out var baseMode))
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeInvalidBaseMode);
        }

        var slugExists = await dbContext.StoreThemes.AnyAsync(t => t.Slug == request.Request.Slug.Trim().ToLowerInvariant() && !t.IsDeleted, cancellationToken);
        if (slugExists)
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeSlugConflict);
        }

        var theme = StoreTheme.Create(request.Request.Name, request.Request.Slug, request.Request.Description, baseMode);
        var tokens = request.Request.Tokens ?? ThemeMapper.DefaultTokens(baseMode);
        theme.CreateDraftVersion(tokens, "Initial draft");

        dbContext.StoreThemes.Add(theme);
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Created, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(theme.Id);
    }
}

public sealed record UpdateThemeCommand(Guid Id, UpdateThemeRequest Request) : IRequest<Result>;

internal sealed class UpdateThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateThemeCommand, Result>
{
    public async Task<Result> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (theme is null)
        {
            return Result.Failure(ThemeErrors.ThemeNotFound);
        }

        theme.Rename(request.Request.Name, null);

        var existingDraft = theme.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault(v => !v.IsPublished);
        var draft = existingDraft ?? theme.CreateDraftVersion(request.Request.Tokens, request.Request.Notes);
        if (existingDraft is null)
        {
            // theme.CreateDraftVersion only appends to the aggregate's in-memory collection; EF's navigation-fixup
            // misclassifies a brand-new child (client-generated Guid key) discovered off an already-tracked parent
            // as Unchanged/Modified rather than Added, so it must be attached explicitly.
            dbContext.ThemeVersions.Add(draft);
        }

        draft.UpdateTokens(request.Request.Tokens, request.Request.Notes);
        var validation = ThemeValidator.Validate(request.Request.Tokens);
        draft.SetValidationWarnings(validation.Warnings);

        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Edited, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteThemeCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteThemeCommand, Result>
{
    public async Task<Result> Handle(DeleteThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure(ThemeErrors.ThemeNotFound);
        }

        if (theme.IsDefault)
        {
            return Result.Failure(ThemeErrors.ThemeDefaultProtected);
        }

        theme.IsDeleted = true;
        theme.DeletedOnUtc = DateTimeOffset.UtcNow;
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Deleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DuplicateThemeCommand(Guid Id, DuplicateThemeRequest Request) : IRequest<Result<Guid>>;

internal sealed class DuplicateThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DuplicateThemeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(DuplicateThemeCommand request, CancellationToken cancellationToken)
    {
        var source = await dbContext.StoreThemes
            .Include(t => t.Versions)
            .Include(t => t.Assets)
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (source is null)
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeNotFound);
        }

        var clone = source.Duplicate(request.Request.NewName, request.Request.NewSlug);
        dbContext.StoreThemes.Add(clone);
        ThemeAuditWriter.Record(dbContext, clone.Id, ThemeAuditAction.Duplicated, currentUser.UserId, $"{{\"sourceId\":\"{source.Id}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(clone.Id);
    }
}

public sealed record PublishThemeCommand(Guid Id, Guid? VersionId) : IRequest<Result<ThemeValidationResultDto>>;

internal sealed class PublishThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<PublishThemeCommand, Result<ThemeValidationResultDto>>
{
    public async Task<Result<ThemeValidationResultDto>> Handle(PublishThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes
            .Include(t => t.Versions)
            .Include(t => t.Assets)
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (theme is null)
        {
            return Result.Failure<ThemeValidationResultDto>(ThemeErrors.ThemeNotFound);
        }

        var version = request.VersionId.HasValue
            ? theme.Versions.FirstOrDefault(v => v.Id == request.VersionId)
            : theme.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        if (version is null)
        {
            return Result.Failure<ThemeValidationResultDto>(ThemeErrors.ThemeNoVersion);
        }

        var assets = theme.Assets.Select(a => new ThemeAssetDto(a.AssetType.ToString(), a.Url, a.AltText)).ToList();
        var validation = ThemeValidator.Validate(version.GetTokens(), assets);
        if (!validation.IsValid)
        {
            return Result.Failure<ThemeValidationResultDto>(new Error("Theme.ValidationFailed", string.Join("; ", validation.Errors)));
        }

        version.SetValidationWarnings(validation.Warnings);
        theme.PublishVersion(version.Id);
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Published, currentUser.UserId, $"{{\"versionId\":\"{version.Id}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(validation);
    }
}

public sealed record ArchiveThemeCommand(Guid Id) : IRequest<Result>;

internal sealed class ArchiveThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ArchiveThemeCommand, Result>
{
    public async Task<Result> Handle(ArchiveThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure(ThemeErrors.ThemeNotFound);
        }

        theme.Archive();
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Archived, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RollbackThemeCommand(Guid Id, Guid VersionId) : IRequest<Result>;

internal sealed class RollbackThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<RollbackThemeCommand, Result>
{
    public async Task<Result> Handle(RollbackThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (theme is null)
        {
            return Result.Failure(ThemeErrors.ThemeNotFound);
        }

        var version = theme.Versions.FirstOrDefault(v => v.Id == request.VersionId && v.IsPublished);
        if (version is null)
        {
            return Result.Failure(ThemeErrors.ThemeNoVersion);
        }

        theme.PublishVersion(version.Id);
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.RolledBack, currentUser.UserId, $"{{\"versionId\":\"{request.VersionId}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record CreatePreviewSessionCommand(Guid ThemeId, Guid? VersionId) : IRequest<Result<ThemePreviewSessionDto>>;

internal sealed class CreatePreviewSessionCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreatePreviewSessionCommand, Result<ThemePreviewSessionDto>>
{
    public async Task<Result<ThemePreviewSessionDto>> Handle(CreatePreviewSessionCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.ThemeId && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure<ThemePreviewSessionDto>(ThemeErrors.ThemeNotFound);
        }

        var versionId = request.VersionId
            ?? await dbContext.ThemeVersions.AsNoTracking()
                .Where(v => v.ThemeId == request.ThemeId)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => v.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (versionId == Guid.Empty)
        {
            return Result.Failure<ThemePreviewSessionDto>(ThemeErrors.ThemeNoVersion);
        }

        var session = ThemePreviewSession.Create(request.ThemeId, versionId, currentUser.UserId, TimeSpan.FromHours(2));
        dbContext.ThemePreviewSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ThemePreviewSessionDto(session.Token, session.ExpiresAtUtc, session.ThemeId, session.VersionId));
    }
}

public sealed record ScheduleThemeCommand(Guid Id, ScheduleThemeRequest Request) : IRequest<Result<Guid>>;

internal sealed class ScheduleThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ScheduleThemeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ScheduleThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeNotFound);
        }

        var schedule = theme.AddSchedule(request.Request.StartsAtUtc, request.Request.EndsAtUtc, request.Request.RecurrenceRule);
        dbContext.ThemeSchedules.Add(schedule);
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Scheduled, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(schedule.Id);
    }
}

public sealed record AssignThemeCommand(Guid Id, AssignThemeRequest Request) : IRequest<Result<Guid>>;

internal sealed class AssignThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<AssignThemeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AssignThemeCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ThemeAssignmentType>(request.Request.AssignmentType, true, out var type))
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeInvalidAssignmentType);
        }

        var theme = await dbContext.StoreThemes.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeNotFound);
        }

        var assignment = theme.AddAssignment(type, request.Request.TargetKey, request.Request.Priority);
        dbContext.ThemeAssignments.Add(assignment);
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Assigned, currentUser.UserId, $"{{\"type\":\"{type}\",\"target\":\"{request.Request.TargetKey}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(assignment.Id);
    }
}

public sealed record UpsertThemeAssetsCommand(Guid Id, IReadOnlyList<ThemeAssetRequest> Assets) : IRequest<Result>;

internal sealed class UpsertThemeAssetsCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpsertThemeAssetsCommand, Result>
{
    public async Task<Result> Handle(UpsertThemeAssetsCommand request, CancellationToken cancellationToken)
    {
        var theme = await dbContext.StoreThemes
            .Include(t => t.Assets)
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);
        if (theme is null)
        {
            return Result.Failure(ThemeErrors.ThemeNotFound);
        }

        foreach (var asset in request.Assets)
        {
            if (!Enum.TryParse<ThemeAssetType>(asset.AssetType, true, out var assetType))
            {
                return Result.Failure(new Error("Theme.InvalidAssetType", $"Invalid asset type: {asset.AssetType}"));
            }

            var isNew = !theme.Assets.Any(a => a.AssetType == assetType);
            var upserted = theme.UpsertAsset(assetType, asset.Url, asset.AltText);
            if (isNew)
            {
                dbContext.ThemeAssets.Add(upserted);
            }
        }

        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Edited, currentUser.UserId, "{\"section\":\"assets\"}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ImportThemeCommand(ImportThemeRequest Request) : IRequest<Result<Guid>>;

internal sealed class ImportThemeCommandHandler(IThemesDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ImportThemeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ImportThemeCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ThemeBaseMode>(request.Request.BaseMode, true, out var baseMode))
        {
            return Result.Failure<Guid>(ThemeErrors.ThemeInvalidBaseMode);
        }

        var theme = StoreTheme.Create(request.Request.Name, request.Request.Slug, null, baseMode);
        theme.CreateDraftVersion(request.Request.Tokens, "Imported theme");

        if (request.Request.Assets is not null)
        {
            foreach (var asset in request.Request.Assets)
            {
                if (Enum.TryParse<ThemeAssetType>(asset.AssetType, true, out var assetType))
                {
                    theme.UpsertAsset(assetType, asset.Url, asset.AltText);
                }
            }
        }

        dbContext.StoreThemes.Add(theme);
        ThemeAuditWriter.Record(dbContext, theme.Id, ThemeAuditAction.Imported, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(theme.Id);
    }
}
