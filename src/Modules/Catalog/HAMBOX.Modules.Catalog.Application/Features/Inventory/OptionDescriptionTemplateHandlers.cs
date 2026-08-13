using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public sealed record SearchOptionDescriptionTemplatesQuery(string? Search) : IRequest<Result<IReadOnlyList<OptionDescriptionTemplateDto>>>;

internal sealed class SearchOptionDescriptionTemplatesQueryHandler : IRequestHandler<SearchOptionDescriptionTemplatesQuery, Result<IReadOnlyList<OptionDescriptionTemplateDto>>>
{
    private readonly ICatalogDbContext _db;

    public SearchOptionDescriptionTemplatesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<OptionDescriptionTemplateDto>>> Handle(SearchOptionDescriptionTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.OptionDescriptionTemplates.AsNoTracking().AsQueryable();

        var search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Name.Contains(search));
        }

        var dtos = await query
            .OrderBy(t => t.Name)
            .Take(20)
            .Select(t => new OptionDescriptionTemplateDto(t.Id, t.Name, t.DescriptionHtml))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<OptionDescriptionTemplateDto>>(dtos);
    }
}

public sealed record GetOptionDescriptionTemplateQuery(Guid TemplateId) : IRequest<Result<OptionDescriptionTemplateDto>>;

internal sealed class GetOptionDescriptionTemplateQueryHandler : IRequestHandler<GetOptionDescriptionTemplateQuery, Result<OptionDescriptionTemplateDto>>
{
    private readonly ICatalogDbContext _db;

    public GetOptionDescriptionTemplateQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<OptionDescriptionTemplateDto>> Handle(GetOptionDescriptionTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionDescriptionTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure<OptionDescriptionTemplateDto>(CatalogErrors.OptionDescriptionTemplateNotFound);
        }

        return Result.Success(new OptionDescriptionTemplateDto(template.Id, template.Name, template.DescriptionHtml));
    }
}

public sealed record CreateOptionDescriptionTemplateCommand(string Name, string DescriptionHtml) : IRequest<Result<Guid>>;

internal sealed class CreateOptionDescriptionTemplateCommandHandler : IRequestHandler<CreateOptionDescriptionTemplateCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateOptionDescriptionTemplateCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateOptionDescriptionTemplateCommand request, CancellationToken cancellationToken)
    {
        var sanitized = ProductOptionDescriptionSanitizer.Sanitize(request.DescriptionHtml);
        if (sanitized is null)
        {
            return Result.Failure<Guid>(CatalogErrors.OptionDescriptionTemplateContentRequired);
        }

        var nameTaken = await _db.OptionDescriptionTemplates.AnyAsync(t => t.Name == request.Name.Trim(), cancellationToken);
        if (nameTaken)
        {
            return Result.Failure<Guid>(CatalogErrors.DuplicateOptionDescriptionTemplateName);
        }

        var template = OptionDescriptionTemplate.Create(request.Name, sanitized);
        _db.OptionDescriptionTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(template.Id);
    }
}

public sealed record UpdateOptionDescriptionTemplateCommand(Guid TemplateId, string Name, string DescriptionHtml) : IRequest<Result>;

internal sealed class UpdateOptionDescriptionTemplateCommandHandler : IRequestHandler<UpdateOptionDescriptionTemplateCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public UpdateOptionDescriptionTemplateCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateOptionDescriptionTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionDescriptionTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(CatalogErrors.OptionDescriptionTemplateNotFound);
        }

        var sanitized = ProductOptionDescriptionSanitizer.Sanitize(request.DescriptionHtml);
        if (sanitized is null)
        {
            return Result.Failure(CatalogErrors.OptionDescriptionTemplateContentRequired);
        }

        var nameTaken = await _db.OptionDescriptionTemplates
            .AnyAsync(t => t.Id != request.TemplateId && t.Name == request.Name.Trim(), cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(CatalogErrors.DuplicateOptionDescriptionTemplateName);
        }

        template.Update(request.Name, sanitized);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteOptionDescriptionTemplateCommand(Guid TemplateId) : IRequest<Result>;

internal sealed class DeleteOptionDescriptionTemplateCommandHandler : IRequestHandler<DeleteOptionDescriptionTemplateCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public DeleteOptionDescriptionTemplateCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteOptionDescriptionTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.OptionDescriptionTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(CatalogErrors.OptionDescriptionTemplateNotFound);
        }

        // Templates are a source snapshot only — nothing on a product option references it, so
        // this can never orphan or change a product option that already copied from it.
        _db.OptionDescriptionTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
