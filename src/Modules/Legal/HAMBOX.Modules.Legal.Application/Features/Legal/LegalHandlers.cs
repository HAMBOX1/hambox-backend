using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Application.Contracts.Legal;
using HAMBOX.Modules.Legal.Application.Errors;
using HAMBOX.Modules.Legal.Application.Services;
using HAMBOX.Modules.Legal.Domain.Legal;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Legal.Application.Features.Legal;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetLegalSectionsQuery(LegalSectionQueryFilter? Filter = null) : IRequest<Result<IReadOnlyList<LegalSectionListItemDto>>>;

internal sealed class GetLegalSectionsQueryHandler(ILegalDbContext dbContext)
    : IRequestHandler<GetLegalSectionsQuery, Result<IReadOnlyList<LegalSectionListItemDto>>>
{
    public async Task<Result<IReadOnlyList<LegalSectionListItemDto>>> Handle(GetLegalSectionsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.LegalSections.AsNoTracking().Include(s => s.Versions).AsQueryable();

        var filter = request.Filter;
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(s => s.Slug.Contains(search) || s.Versions.Any(v => v.TitleEn.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Category))
            {
                query = query.Where(s => s.Category == filter.Category);
            }

            if (filter.ShowInFooter.HasValue)
            {
                query = query.Where(s => s.ShowInFooter == filter.ShowInFooter.Value);
            }

            if (filter.ShowInNavigation.HasValue)
            {
                query = query.Where(s => s.ShowInNavigation == filter.ShowInNavigation.Value);
            }

            if (filter.RequireAcceptance.HasValue)
            {
                query = query.Where(s => s.RequireAcceptance == filter.RequireAcceptance.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = filter.Status switch
                {
                    "Archived" => query.Where(s => s.IsArchived),
                    "Published" => query.Where(s => !s.IsArchived && s.PublishedVersionId != null),
                    "Draft" => query.Where(s => !s.IsArchived && s.PublishedVersionId == null),
                    _ => query,
                };
            }
        }

        var sections = await query.OrderBy(s => s.SortOrder).ThenBy(s => s.Slug).ToListAsync(cancellationToken);
        var items = sections.Select(LegalMapper.ToListItem).ToList();
        return Result.Success<IReadOnlyList<LegalSectionListItemDto>>(items);
    }
}

public sealed record GetLegalSectionBySlugQuery(string Slug) : IRequest<Result<LegalSectionDetailDto>>;

internal sealed class GetLegalSectionBySlugQueryHandler(ILegalDbContext dbContext)
    : IRequestHandler<GetLegalSectionBySlugQuery, Result<LegalSectionDetailDto>>
{
    public async Task<Result<LegalSectionDetailDto>> Handle(GetLegalSectionBySlugQuery request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections.AsNoTracking()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Slug == request.Slug, cancellationToken);

        return section is null
            ? Result.Failure<LegalSectionDetailDto>(LegalErrors.SectionNotFound)
            : Result.Success(LegalMapper.ToDetail(section));
    }
}

public sealed record GetLegalSectionHistoryQuery(string Slug) : IRequest<Result<IReadOnlyList<LegalSectionHistoryEntryDto>>>;

internal sealed class GetLegalSectionHistoryQueryHandler(ILegalDbContext dbContext)
    : IRequestHandler<GetLegalSectionHistoryQuery, Result<IReadOnlyList<LegalSectionHistoryEntryDto>>>
{
    public async Task<Result<IReadOnlyList<LegalSectionHistoryEntryDto>>> Handle(GetLegalSectionHistoryQuery request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == request.Slug, cancellationToken);

        if (section is null)
        {
            return Result.Failure<IReadOnlyList<LegalSectionHistoryEntryDto>>(LegalErrors.SectionNotFound);
        }

        var history = await dbContext.LegalSectionAuditLogs.AsNoTracking()
            .Where(l => l.LegalSectionId == section.Id)
            .OrderByDescending(l => l.CreatedOnUtc)
            .Select(l => new LegalSectionHistoryEntryDto(l.Id, l.Action.ToString(), l.ActorUserId, l.CreatedOnUtc, l.DetailsJson))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LegalSectionHistoryEntryDto>>(history);
    }
}

public sealed record GetPublishedLegalSectionsQuery : IRequest<Result<IReadOnlyList<PublicLegalSectionSummaryDto>>>;

internal sealed class GetPublishedLegalSectionsQueryHandler(ILegalDbContext dbContext)
    : IRequestHandler<GetPublishedLegalSectionsQuery, Result<IReadOnlyList<PublicLegalSectionSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<PublicLegalSectionSummaryDto>>> Handle(GetPublishedLegalSectionsQuery request, CancellationToken cancellationToken)
    {
        var sections = await dbContext.LegalSections.AsNoTracking()
            .Include(s => s.Versions)
            .Where(s => !s.IsArchived && s.PublishedVersionId != null)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Slug)
            .ToListAsync(cancellationToken);

        var items = sections
            .Select(s => new { Section = s, Published = s.Versions.FirstOrDefault(v => v.Id == s.PublishedVersionId) })
            .Where(x => x.Published is not null)
            .Select(x => LegalMapper.ToPublicSummary(x.Section, x.Published!))
            .ToList();

        return Result.Success<IReadOnlyList<PublicLegalSectionSummaryDto>>(items);
    }
}

public sealed record GetPublishedLegalSectionBySlugQuery(string Slug) : IRequest<Result<PublicLegalSectionDto>>;

internal sealed class GetPublishedLegalSectionBySlugQueryHandler(ILegalDbContext dbContext)
    : IRequestHandler<GetPublishedLegalSectionBySlugQuery, Result<PublicLegalSectionDto>>
{
    public async Task<Result<PublicLegalSectionDto>> Handle(GetPublishedLegalSectionBySlugQuery request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections.AsNoTracking()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Slug == request.Slug && !s.IsArchived, cancellationToken);

        var published = section?.PublishedVersionId is Guid versionId
            ? section.Versions.FirstOrDefault(v => v.Id == versionId)
            : null;

        return published is null
            ? Result.Failure<PublicLegalSectionDto>(LegalErrors.NoVersion)
            : Result.Success(LegalMapper.ToPublicDetail(section!, published));
    }
}

public sealed record GetLegalAcceptanceStatusQuery : IRequest<Result<LegalAcceptanceStatusDto>>;

internal sealed class GetLegalAcceptanceStatusQueryHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<GetLegalAcceptanceStatusQuery, Result<LegalAcceptanceStatusDto>>
{
    public async Task<Result<LegalAcceptanceStatusDto>> Handle(GetLegalAcceptanceStatusQuery request, CancellationToken cancellationToken)
    {
        var staleSlugs = await LegalAcceptanceRecorder.GetStaleSlugsAsync(dbContext, currentUser.UserId!, cancellationToken);
        return Result.Success(new LegalAcceptanceStatusDto(staleSlugs.Count > 0, staleSlugs));
    }
}

// ─── Commands ──────────────────────────────────────────────────

public sealed record CreateLegalSectionCommand(CreateLegalSectionRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateLegalSectionCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateLegalSectionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateLegalSectionCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Request.Slug.Trim().ToLowerInvariant();

        var exists = await dbContext.LegalSections.AnyAsync(s => s.Slug == slug, cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(LegalErrors.SlugAlreadyExists);
        }

        var section = LegalSection.Create(slug);
        section.UpdateMetadata(
            slug,
            request.Request.Category,
            request.Request.Icon,
            sortOrder: 0,
            descriptionEn: null,
            descriptionAr: null,
            seoTitle: null,
            seoDescription: null,
            seoKeywords: null,
            request.Request.ShowInFooter,
            request.Request.ShowInNavigation,
            request.Request.RequireAcceptance);
        section.CreateDraftVersion(request.Request.TitleEn, request.Request.TitleAr, string.Empty, null, null);

        dbContext.LegalSections.Add(section);
        LegalAuditWriter.Record(dbContext, section.Id, LegalSectionAuditAction.Created, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(section.Id);
    }
}

public sealed record UpdateLegalSectionCommand(string Slug, UpdateLegalSectionRequest Request) : IRequest<Result>;

internal sealed class UpdateLegalSectionCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateLegalSectionCommand, Result>
{
    public async Task<Result> Handle(UpdateLegalSectionCommand request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Slug == request.Slug, cancellationToken);

        if (section is null)
        {
            return Result.Failure(LegalErrors.SectionNotFound);
        }

        var newSlug = request.Request.Slug.Trim().ToLowerInvariant();
        if (newSlug != section.Slug)
        {
            var slugTaken = await dbContext.LegalSections.AnyAsync(s => s.Slug == newSlug && s.Id != section.Id, cancellationToken);
            if (slugTaken)
            {
                return Result.Failure(LegalErrors.SlugAlreadyExists);
            }
        }

        section.UpdateMetadata(
            newSlug,
            request.Request.Category,
            request.Request.Icon,
            request.Request.SortOrder,
            request.Request.DescriptionEn,
            request.Request.DescriptionAr,
            request.Request.SeoTitle,
            request.Request.SeoDescription,
            request.Request.SeoKeywords,
            request.Request.ShowInFooter,
            request.Request.ShowInNavigation,
            request.Request.RequireAcceptance);

        var draft = section.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault(v => !v.IsPublished);
        if (draft is null)
        {
            draft = section.CreateDraftVersion(
                request.Request.TitleEn, request.Request.TitleAr, request.Request.ContentEn, request.Request.ContentAr, request.Request.VersionNotes);
            dbContext.LegalSectionVersions.Add(draft);
        }
        else
        {
            draft.UpdateContent(request.Request.TitleEn, request.Request.TitleAr, request.Request.ContentEn, request.Request.ContentAr, request.Request.VersionNotes);
        }

        LegalAuditWriter.Record(dbContext, section.Id, LegalSectionAuditAction.Updated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record PublishLegalSectionCommand(string Slug, Guid? VersionId) : IRequest<Result>;

internal sealed class PublishLegalSectionCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<PublishLegalSectionCommand, Result>
{
    public async Task<Result> Handle(PublishLegalSectionCommand request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Slug == request.Slug, cancellationToken);

        if (section is null)
        {
            return Result.Failure(LegalErrors.SectionNotFound);
        }

        // A missing/empty JSON body binds [FromBody] Guid? to Guid.Empty rather than null,
        // so treat Guid.Empty the same as "no specific version requested".
        var requestedVersionId = request.VersionId is { } id && id != Guid.Empty ? id : (Guid?)null;

        // "No specific version requested" means "publish the current draft" — the same
        // unpublished version Update edits — not simply whichever version has the highest
        // VersionNumber, since an older draft can coexist with a newer already-published version.
        var version = requestedVersionId.HasValue
            ? section.Versions.FirstOrDefault(v => v.Id == requestedVersionId)
            : section.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault(v => !v.IsPublished);

        if (version is null)
        {
            return Result.Failure(LegalErrors.NoVersion);
        }

        // An explicit versionId means the admin picked a specific (usually older) version from
        // history — that's a restore, distinct from publishing whatever the current draft is.
        var action = requestedVersionId.HasValue ? LegalSectionAuditAction.Restored : LegalSectionAuditAction.Published;
        section.PublishVersion(version.Id, currentUser.UserId);
        LegalAuditWriter.Record(dbContext, section.Id, action, currentUser.UserId, $"{{\"versionId\":\"{version.Id}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ArchiveLegalSectionCommand(string Slug, bool Archived) : IRequest<Result>;

internal sealed class ArchiveLegalSectionCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ArchiveLegalSectionCommand, Result>
{
    public async Task<Result> Handle(ArchiveLegalSectionCommand request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections.FirstOrDefaultAsync(s => s.Slug == request.Slug, cancellationToken);
        if (section is null)
        {
            return Result.Failure(LegalErrors.SectionNotFound);
        }

        section.SetArchived(request.Archived);
        LegalAuditWriter.Record(
            dbContext, section.Id, request.Archived ? LegalSectionAuditAction.Archived : LegalSectionAuditAction.Unarchived, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DuplicateLegalSectionCommand(string SourceSlug, DuplicateLegalSectionRequest Request) : IRequest<Result<string>>;

internal sealed class DuplicateLegalSectionCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DuplicateLegalSectionCommand, Result<string>>
{
    public async Task<Result<string>> Handle(DuplicateLegalSectionCommand request, CancellationToken cancellationToken)
    {
        var source = await dbContext.LegalSections
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Slug == request.SourceSlug, cancellationToken);

        if (source is null)
        {
            return Result.Failure<string>(LegalErrors.SectionNotFound);
        }

        var newSlug = request.Request.NewSlug.Trim().ToLowerInvariant();
        var slugTaken = await dbContext.LegalSections.AnyAsync(s => s.Slug == newSlug, cancellationToken);
        if (slugTaken)
        {
            return Result.Failure<string>(LegalErrors.SlugAlreadyExists);
        }

        var sourceVersion = source.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        var copy = LegalSection.Create(newSlug);
        copy.UpdateMetadata(
            newSlug,
            source.Category,
            source.Icon,
            source.SortOrder,
            source.DescriptionEn,
            source.DescriptionAr,
            source.SeoTitle,
            source.SeoDescription,
            source.SeoKeywords,
            source.ShowInFooter,
            source.ShowInNavigation,
            source.RequireAcceptance);
        copy.CreateDraftVersion(
            request.Request.NewTitleEn ?? sourceVersion?.TitleEn ?? newSlug,
            sourceVersion?.TitleAr,
            sourceVersion?.ContentEn ?? string.Empty,
            sourceVersion?.ContentAr,
            $"Duplicated from '{source.Slug}'.");

        dbContext.LegalSections.Add(copy);
        LegalAuditWriter.Record(dbContext, copy.Id, LegalSectionAuditAction.Duplicated, currentUser.UserId, $"{{\"sourceSlug\":\"{source.Slug}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(newSlug);
    }
}

public sealed record DeleteLegalSectionCommand(string Slug) : IRequest<Result>;

internal sealed class DeleteLegalSectionCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteLegalSectionCommand, Result>
{
    public async Task<Result> Handle(DeleteLegalSectionCommand request, CancellationToken cancellationToken)
    {
        var section = await dbContext.LegalSections.FirstOrDefaultAsync(s => s.Slug == request.Slug, cancellationToken);
        if (section is null)
        {
            return Result.Failure(LegalErrors.SectionNotFound);
        }

        LegalAuditWriter.Record(dbContext, section.Id, LegalSectionAuditAction.Deleted, currentUser.UserId);
        dbContext.LegalSections.Remove(section);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RecordLegalAcceptanceCommand(string IpAddress, string UserAgent, string Language) : IRequest<Result>;

internal sealed class RecordLegalAcceptanceCommandHandler(ILegalDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<RecordLegalAcceptanceCommand, Result>
{
    public async Task<Result> Handle(RecordLegalAcceptanceCommand request, CancellationToken cancellationToken)
    {
        await LegalAcceptanceRecorder.RecordAsync(
            dbContext,
            currentUser.UserId!,
            request.IpAddress,
            request.UserAgent,
            request.Language,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
