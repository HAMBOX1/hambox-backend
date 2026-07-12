using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotions;

public sealed record GetPromotionsQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? Status,
    string? Type) : IRequest<Result<PagedResult<PromotionListItemDto>>>;

internal sealed class GetPromotionsQueryHandler : IRequestHandler<GetPromotionsQuery, Result<PagedResult<PromotionListItemDto>>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetPromotionsQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PagedResult<PromotionListItemDto>>> Handle(
        GetPromotionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Promotions
            .Include(p => p.CouponCodes)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(p => p.Name.Contains(term));
        }

        if (PromotionMapper.ParseStatusFilter(request.Status) is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            !request.Type.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<PromotionType>(request.Type, ignoreCase: true, out var type))
        {
            query = query.Where(p => p.Type == type);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<PromotionListItemDto>(
            items.Select(PromotionMapper.ToListItem).ToList(),
            request.PageNumber,
            request.PageSize,
            total));
    }
}
