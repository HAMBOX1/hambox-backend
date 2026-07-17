using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Contracts;
using HAMBOX.Modules.Communication.Application.Errors;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Application.Features.Templates;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetCommunicationTemplatesQuery : IRequest<Result<IReadOnlyList<CommunicationTemplateListItemDto>>>;

internal sealed class GetCommunicationTemplatesQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetCommunicationTemplatesQuery, Result<IReadOnlyList<CommunicationTemplateListItemDto>>>
{
    public async Task<Result<IReadOnlyList<CommunicationTemplateListItemDto>>> Handle(GetCommunicationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await db.CommunicationTemplates.AsNoTracking()
            .Include(t => t.Versions)
            .OrderBy(t => t.Key).ThenBy(t => t.Channel)
            .ToListAsync(cancellationToken);

        var items = templates.Select(ToListItem).ToList();
        return Result.Success<IReadOnlyList<CommunicationTemplateListItemDto>>(items);
    }

    internal static CommunicationTemplateListItemDto ToListItem(CommunicationTemplate t)
    {
        var published = t.Versions.FirstOrDefault(v => v.Id == t.ActiveVersionId);
        var hasDraft = t.Versions.Any(v => !v.IsPublished);
        return new CommunicationTemplateListItemDto(
            t.Id, t.Key, t.Channel, t.Category.ToString(),
            published is not null, published?.VersionNumber, hasDraft, t.Versions.Count);
    }
}

public sealed record GetCommunicationTemplateQuery(Guid Id) : IRequest<Result<CommunicationTemplateDetailDto>>;

internal sealed class GetCommunicationTemplateQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetCommunicationTemplateQuery, Result<CommunicationTemplateDetailDto>>
{
    public async Task<Result<CommunicationTemplateDetailDto>> Handle(GetCommunicationTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await db.CommunicationTemplates.AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        return template is null
            ? Result.Failure<CommunicationTemplateDetailDto>(CommunicationErrors.TemplateNotFound)
            : Result.Success(ToDetail(template));
    }

    internal static CommunicationTemplateDetailDto ToDetail(CommunicationTemplate t) => new(
        t.Id, t.Key, t.Channel, t.Category.ToString(), t.ActiveVersionId,
        t.Versions.OrderByDescending(v => v.VersionNumber).Select(v => new CommunicationTemplateVersionDto(
            v.Id, v.VersionNumber, v.SubjectEn, v.SubjectAr, v.BodyEn, v.BodyAr, v.VariablesJson,
            v.IsPublished, v.PublishedOnUtc, v.PublishedBy)).ToList());
}

public sealed record PreviewCommunicationTemplateQuery(Guid Id, Guid? VersionId, IReadOnlyDictionary<string, string> Variables)
    : IRequest<Result<CommunicationTemplatePreviewDto>>;

internal sealed class PreviewCommunicationTemplateQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<PreviewCommunicationTemplateQuery, Result<CommunicationTemplatePreviewDto>>
{
    public async Task<Result<CommunicationTemplatePreviewDto>> Handle(PreviewCommunicationTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await db.CommunicationTemplates.AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        var version = request.VersionId.HasValue
            ? template?.Versions.FirstOrDefault(v => v.Id == request.VersionId)
            : template?.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        if (version is null)
        {
            return Result.Failure<CommunicationTemplatePreviewDto>(CommunicationErrors.TemplateVersionNotFound);
        }

        var subject = version.SubjectEn;
        var body = version.BodyEn;
        foreach (var (key, value) in request.Variables)
        {
            subject = subject.Replace($"{{{{{key}}}}}", value);
            body = body.Replace($"{{{{{key}}}}}", value);
        }

        return Result.Success(new CommunicationTemplatePreviewDto(subject, body));
    }
}

// ─── Commands ──────────────────────────────────────────────────

public sealed record CreateCommunicationTemplateCommand(CreateCommunicationTemplateRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateCommunicationTemplateCommandHandler(ICommunicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateCommunicationTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCommunicationTemplateCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!Enum.TryParse<CommunicationCategory>(req.Category, true, out var category))
        {
            return Result.Failure<Guid>(CommunicationErrors.TemplateNotFound);
        }

        var exists = await db.CommunicationTemplates.AnyAsync(t => t.Key == req.Key && t.Channel == req.Channel, cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(CommunicationErrors.TemplateNotFound);
        }

        var template = CommunicationTemplate.Create(req.Key, req.Channel, category);
        template.CreateDraftVersion(req.Key, null, string.Empty, null, null);
        db.CommunicationTemplates.Add(template);

        db.CommunicationAuditLogs.Add(CommunicationAuditLog.Create(
            CommunicationAuditAction.TemplateCreated, currentUser.UserId, "CommunicationTemplate", template.Id.ToString()));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(template.Id);
    }
}

public sealed record UpdateCommunicationTemplateCommand(Guid Id, UpdateCommunicationTemplateRequest Request) : IRequest<Result>;

internal sealed class UpdateCommunicationTemplateCommandHandler(ICommunicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateCommunicationTemplateCommand, Result>
{
    public async Task<Result> Handle(UpdateCommunicationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await db.CommunicationTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
        {
            return Result.Failure(CommunicationErrors.TemplateNotFound);
        }

        var req = request.Request;
        var draft = template.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault(v => !v.IsPublished);
        if (draft is null)
        {
            template.CreateDraftVersion(req.SubjectEn, req.SubjectAr, req.BodyEn, req.BodyAr, req.VariablesJson);
        }
        else
        {
            draft.UpdateContent(req.SubjectEn, req.SubjectAr, req.BodyEn, req.BodyAr, req.VariablesJson);
        }

        db.CommunicationAuditLogs.Add(CommunicationAuditLog.Create(
            CommunicationAuditAction.TemplateUpdated, currentUser.UserId, "CommunicationTemplate", template.Id.ToString()));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record PublishCommunicationTemplateCommand(Guid Id, Guid? VersionId) : IRequest<Result>;

internal sealed class PublishCommunicationTemplateCommandHandler(ICommunicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<PublishCommunicationTemplateCommand, Result>
{
    public async Task<Result> Handle(PublishCommunicationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await db.CommunicationTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
        {
            return Result.Failure(CommunicationErrors.TemplateNotFound);
        }

        var version = request.VersionId.HasValue
            ? template.Versions.FirstOrDefault(v => v.Id == request.VersionId)
            : template.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        if (version is null)
        {
            return Result.Failure(CommunicationErrors.TemplateVersionNotFound);
        }

        template.PublishVersion(version.Id, currentUser.UserId);

        db.CommunicationAuditLogs.Add(CommunicationAuditLog.Create(
            CommunicationAuditAction.TemplatePublished, currentUser.UserId, "CommunicationTemplate", template.Id.ToString(),
            $"{{\"versionId\":\"{version.Id}\"}}"));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
