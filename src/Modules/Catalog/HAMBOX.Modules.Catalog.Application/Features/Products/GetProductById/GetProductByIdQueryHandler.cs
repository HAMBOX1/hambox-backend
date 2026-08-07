using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;

internal sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly ICatalogDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IMembershipAccessProvider _membershipAccess;

    public GetProductByIdQueryHandler(
        ICatalogDbContext dbContext,
        ICurrentUserService currentUser,
        IMembershipAccessProvider membershipAccess)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _membershipAccess = membershipAccess;
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(entry => entry.Images)
            .Include(entry => entry.AdditionalCategories)
            .Include(entry => entry.Collections)
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDto>(CatalogErrors.ProductNotFound);
        }

        var category = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == product.CategoryId)
            .Select(c => new { c.NameEn, c.NameAr, c.ImageUrl, c.EffectiveImageUrl })
            .FirstOrDefaultAsync(cancellationToken);

        var productAccess = await _membershipAccess.GetProductAccessAsync(_currentUser.UserId, product.Id, cancellationToken);
        var canPurchase = productAccess.HasAccess && IsReleasedFor(product.PublicReleaseOnUtc,
            await _membershipAccess.GetAccessInfoAsync(_currentUser.UserId, cancellationToken));

        return Result.Success(CatalogMapper.ToProductDto(
            product,
            category?.NameEn ?? string.Empty,
            category?.NameAr ?? string.Empty,
            includeImages: true,
            categoryOwnImageUrl: category?.ImageUrl,
            categoryEffectiveImageUrl: category?.EffectiveImageUrl,
            isMembersOnly: productAccess.IsRestricted,
            canPurchase: canPurchase,
            requiredPlanNames: productAccess.RequiredPlanNames));
    }

    internal static bool IsReleasedFor(DateTime? publicReleaseOnUtc, MembershipAccessInfo access)
    {
        if (publicReleaseOnUtc is not DateTime releaseOnUtc || releaseOnUtc <= DateTime.UtcNow)
        {
            return true;
        }

        return DateTime.UtcNow >= releaseOnUtc.AddDays(-access.EarlyAccessDays);
    }
}
