using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.CreateCategory;

internal sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _dbContext;

    public CreateCategoryCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await _dbContext.Categories.AnyAsync(c => c.Slug == request.Slug, cancellationToken);
        if (slugExists)
        {
            return Result.Failure<Guid>(CatalogErrors.CategorySlugNotUnique);
        }

        var parentError = await CategoryParentValidator.ValidateParentAsync(
            _dbContext,
            Guid.Empty,
            request.ParentId,
            cancellationToken);

        if (parentError is not null)
        {
            return Result.Failure<Guid>(parentError.Error);
        }

        var siblingCount = await _dbContext.Categories
            .CountAsync(c => c.ParentId == request.ParentId, cancellationToken);

        var category = Category.Create(request.NameAr, request.NameEn, request.Slug, request.ParentId);
        category.SetSortOrder(siblingCount);
        category.SetEffectiveImageUrl(
            await CategoryImageResolution.ResolveParentEffectiveImageAsync(_dbContext, request.ParentId, cancellationToken));
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Id);
    }
}
