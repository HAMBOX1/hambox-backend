using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.UpdateCategory;

internal sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public UpdateCategoryCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(CatalogErrors.CategoryNotFound);
        }

        var slugInUse = await _dbContext.Categories.AnyAsync(c => c.Id != request.Id && c.Slug == request.Slug, cancellationToken);
        if (slugInUse)
        {
            return Result.Failure(CatalogErrors.CategorySlugNotUnique);
        }

        var parentError = await CategoryParentValidator.ValidateParentAsync(
            _dbContext,
            request.Id,
            request.ParentId,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        var parentChanged = category.ParentId != request.ParentId;

        category.Update(request.NameAr, request.NameEn, request.Slug, request.ParentId);

        if (parentChanged)
        {
            await CategoryImageResolution.ApplyAndPropagateAsync(_dbContext, category, cancellationToken);
        }

        if (request.IsActive && !category.IsActive)
        {
            category.Activate();
        }
        else if (!request.IsActive && category.IsActive)
        {
            category.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
