using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>How to handle importing a template into a product that already has a root option
/// group with the same derived key.</summary>
public enum ImportConflictResolution
{
    /// <summary>Import as a new, separate group — the derived key is auto-suffixed if it collides.</summary>
    AddSeparate = 0,

    /// <summary>Force-delete the existing group (through the same usage-safety gate as a direct
    /// delete) and import fresh in its place.</summary>
    Replace = 1,
}

public sealed record OptionGroupTemplateOptionInput(string Value, string Label, int SortOrder, string? DescriptionHtml);

public sealed record SearchOptionGroupTemplatesQuery(string? Search) : IRequest<Result<IReadOnlyList<OptionGroupTemplateSummaryDto>>>;

internal sealed class SearchOptionGroupTemplatesQueryHandler : IRequestHandler<SearchOptionGroupTemplatesQuery, Result<IReadOnlyList<OptionGroupTemplateSummaryDto>>>
{
    private readonly ICatalogDbContext _db;

    public SearchOptionGroupTemplatesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<OptionGroupTemplateSummaryDto>>> Handle(SearchOptionGroupTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.OptionGroupTemplates.AsNoTracking().Include(t => t.Options).AsQueryable();

        var search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Name.Contains(search));
        }

        var templates = await query
            .OrderBy(t => t.Name)
            .Take(20)
            .ToListAsync(cancellationToken);

        var dtos = templates
            .Select(t => new OptionGroupTemplateSummaryDto(t.Id, t.Name, t.Options.Count))
            .ToList();

        return Result.Success<IReadOnlyList<OptionGroupTemplateSummaryDto>>(dtos);
    }
}

public sealed record GetOptionGroupTemplateQuery(Guid TemplateId) : IRequest<Result<OptionGroupTemplateDto>>;

internal sealed class GetOptionGroupTemplateQueryHandler : IRequestHandler<GetOptionGroupTemplateQuery, Result<OptionGroupTemplateDto>>
{
    private readonly ICatalogDbContext _db;

    public GetOptionGroupTemplateQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<OptionGroupTemplateDto>> Handle(GetOptionGroupTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionGroupTemplates
            .AsNoTracking()
            .Include(t => t.Options)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure<OptionGroupTemplateDto>(CatalogErrors.OptionGroupTemplateNotFound);
        }

        var dto = new OptionGroupTemplateDto(
            template.Id,
            template.Name,
            template.IsRequiredDefault,
            template.Options.OrderBy(o => o.SortOrder)
                .Select(o => new OptionGroupTemplateOptionDto(o.Id, o.Value, o.Label, o.SortOrder, o.DescriptionHtml))
                .ToList());

        return Result.Success(dto);
    }
}

public sealed record SaveOptionGroupAsTemplateCommand(Guid OptionGroupId, string Name) : IRequest<Result<Guid>>;

internal sealed class SaveOptionGroupAsTemplateCommandHandler : IRequestHandler<SaveOptionGroupAsTemplateCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public SaveOptionGroupAsTemplateCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(SaveOptionGroupAsTemplateCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .FirstOrDefaultAsync(g => g.Id == request.OptionGroupId, cancellationToken);

        if (group is null)
        {
            return Result.Failure<Guid>(CatalogErrors.OptionGroupNotFound);
        }

        var optionIds = group.Options.Select(o => o.Id).ToList();
        var hasChildGroups = await _db.ProductOptionGroups
            .AnyAsync(g => g.ParentOptionId != null && optionIds.Contains(g.ParentOptionId.Value), cancellationToken);
        if (hasChildGroups)
        {
            return Result.Failure<Guid>(CatalogErrors.OptionGroupHasChildGroups);
        }

        var nameTaken = await _db.OptionGroupTemplates.AnyAsync(t => t.Name == request.Name.Trim(), cancellationToken);
        if (nameTaken)
        {
            return Result.Failure<Guid>(CatalogErrors.DuplicateOptionGroupTemplateName);
        }

        var template = OptionGroupTemplate.Create(request.Name, group.IsRequired);
        template.ReplaceOptions(group.Options.OrderBy(o => o.SortOrder)
            .Select(o => (o.Value, o.Label, o.SortOrder, o.DescriptionHtml)));

        _db.OptionGroupTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(template.Id);
    }
}

public sealed record UpdateOptionGroupTemplateCommand(
    Guid TemplateId,
    string Name,
    bool IsRequiredDefault,
    IReadOnlyList<OptionGroupTemplateOptionInput> Options) : IRequest<Result>;

internal sealed class UpdateOptionGroupTemplateCommandHandler : IRequestHandler<UpdateOptionGroupTemplateCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public UpdateOptionGroupTemplateCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateOptionGroupTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionGroupTemplates
            .Include(t => t.Options)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure(CatalogErrors.OptionGroupTemplateNotFound);
        }

        var nameTaken = await _db.OptionGroupTemplates
            .AnyAsync(t => t.Id != request.TemplateId && t.Name == request.Name.Trim(), cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(CatalogErrors.DuplicateOptionGroupTemplateName);
        }

        template.Update(request.Name, request.IsRequiredDefault);
        var created = template.ReplaceOptions(request.Options
            .OrderBy(o => o.SortOrder)
            .Select(o => (o.Value, o.Label, o.SortOrder, ProductOptionDescriptionSanitizer.Sanitize(o.DescriptionHtml))));

        // template is already tracked (loaded above) — EF's automatic graph fixup on an
        // already-tracked parent misjudges these client-Guid-keyed new children as "Modified"
        // rather than "Added" (see ReplaceOptions doc comment), so track them explicitly.
        _db.OptionGroupTemplateOptions.AddRange(created);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteOptionGroupTemplateCommand(Guid TemplateId) : IRequest<Result>;

internal sealed class DeleteOptionGroupTemplateCommandHandler : IRequestHandler<DeleteOptionGroupTemplateCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public DeleteOptionGroupTemplateCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteOptionGroupTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionGroupTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(CatalogErrors.OptionGroupTemplateNotFound);
        }

        // Templates are a source snapshot only — nothing on a product references it, so this can
        // never orphan or break a product that already imported it.
        _db.OptionGroupTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ImportOptionGroupTemplateCommand(
    Guid ProductId,
    Guid TemplateId,
    ImportConflictResolution Resolution) : IRequest<Result<Guid>>;

internal sealed class ImportOptionGroupTemplateCommandHandler : IRequestHandler<ImportOptionGroupTemplateCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public ImportOptionGroupTemplateCommandHandler(ICatalogDbContext db, ISender sender, ICurrentUserService currentUser)
    {
        _db = db;
        _sender = sender;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(ImportOptionGroupTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionGroupTemplates
            .AsNoTracking()
            .Include(t => t.Options)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure<Guid>(CatalogErrors.OptionGroupTemplateNotFound);
        }

        var baseKey = Slugify(template.Name);
        var key = baseKey;

        var existing = await _db.ProductOptionGroups
            .FirstOrDefaultAsync(g => g.ProductId == request.ProductId && g.ParentOptionId == null && g.Key == key, cancellationToken);

        if (existing is not null && request.Resolution == ImportConflictResolution.Replace)
        {
            var deleteResult = await _sender.Send(new DeleteProductOptionGroupCommand(existing.Id, Force: true), cancellationToken);
            if (deleteResult.IsFailure)
            {
                return Result.Failure<Guid>(deleteResult.Error);
            }

            existing = null;
        }

        if (existing is not null)
        {
            // AddSeparate with a colliding key — auto-suffix rather than fail, since the admin
            // already chose "add as separate group" explicitly.
            var suffix = 2;
            while (await _db.ProductOptionGroups.AnyAsync(g => g.ProductId == request.ProductId && g.ParentOptionId == null && g.Key == $"{baseKey}-{suffix}", cancellationToken))
            {
                suffix++;
            }

            key = $"{baseKey}-{suffix}";
        }

        var group = ProductOptionGroup.Create(request.ProductId, key, template.Name, sortOrder: 0, isRequired: template.IsRequiredDefault);
        _db.ProductOptionGroups.Add(group);

        foreach (var templateOption in template.Options.OrderBy(o => o.SortOrder))
        {
            var option = ProductOption.Create(group.Id, templateOption.Value, templateOption.Label, templateOption.SortOrder, templateOption.DescriptionHtml);
            _db.ProductOptions.Add(option);
        }

        _db.InventoryAuditLogs.Add(InventoryAuditLog.Create(
            InventoryAuditAction.OptionGroupCreated,
            productId: request.ProductId,
            performedByUserId: _currentUser.UserId,
            details: $"Imported from saved group '{template.Name}'"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(group.Id);
    }

    private static string Slugify(string value)
    {
        var trimmedLower = value.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(trimmedLower.Length);
        var lastWasDash = false;

        foreach (var ch in trimmedLower)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
