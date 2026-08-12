using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqs;

internal sealed class GetFaqsQueryHandler(IContentDbContext dbContext, ICatalogDbContext catalogDbContext)
    : IRequestHandler<GetFaqsQuery, Result<PagedResult<FaqDto>>>
{
    public async Task<Result<PagedResult<FaqDto>>> Handle(GetFaqsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Faqs.AsNoTracking();

        if (request.Scope is { } scope)
        {
            query = query.Where(f => f.Scope == scope);
        }

        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(f => f.CategoryId == categoryId);
        }

        if (request.IsPublished is { } isPublished)
        {
            query = query.Where(f => f.IsPublished == isPublished);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(f =>
                EF.Functions.Like(f.QuestionEn, $"%{term}%") ||
                (f.QuestionAr != null && EF.Functions.Like(f.QuestionAr, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;

        var faqs = await query
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.ModifiedOnUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = await MapToDtosAsync(faqs, dbContext, catalogDbContext, cancellationToken);

        return Result.Success(new PagedResult<FaqDto>(dtos, pageNumber, pageSize, totalCount));
    }

    /// <summary>
    /// Resolves category names and Product/Category target labels for a page of FAQs with exactly three
    /// round trips total (categories, target products, target categories) regardless of page size —
    /// avoids N+1 lookups.
    /// </summary>
    internal static async Task<IReadOnlyList<FaqDto>> MapToDtosAsync(
        IReadOnlyList<Faq> faqs,
        IContentDbContext dbContext,
        ICatalogDbContext catalogDbContext,
        CancellationToken cancellationToken)
    {
        if (faqs.Count == 0)
        {
            return [];
        }

        var categoryIds = faqs.Select(f => f.CategoryId).Distinct().ToList();
        var categoryNameById = await dbContext.FaqCategories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameEn, cancellationToken);

        var productTargetIds = faqs.Where(f => f.Scope == FaqScope.Product).Select(f => f.TargetId!.Value).Distinct().ToList();
        var categoryTargetIds = faqs.Where(f => f.Scope == FaqScope.Category).Select(f => f.TargetId!.Value).Distinct().ToList();

        Dictionary<Guid, string> productNameById = productTargetIds.Count == 0
            ? []
            : await catalogDbContext.Products
                .AsNoTracking()
                .Where(p => productTargetIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);

        Dictionary<Guid, string> categoryTargetNameById = categoryTargetIds.Count == 0
            ? []
            : await catalogDbContext.Categories
                .AsNoTracking()
                .Where(c => categoryTargetIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.NameEn, cancellationToken);

        return faqs
            .Select(f => new FaqDto(
                f.Id,
                f.QuestionEn,
                f.QuestionAr,
                f.AnswerEn,
                f.AnswerAr,
                f.CategoryId,
                categoryNameById.GetValueOrDefault(f.CategoryId, "—"),
                f.Scope,
                f.TargetId,
                f.Scope switch
                {
                    FaqScope.Product => f.TargetId is { } pid ? productNameById.GetValueOrDefault(pid) : null,
                    FaqScope.Category => f.TargetId is { } cid ? categoryTargetNameById.GetValueOrDefault(cid) : null,
                    _ => null,
                },
                f.SortOrder,
                f.IsPublished,
                f.PublishedOnUtc?.UtcDateTime,
                (f.ModifiedOnUtc ?? f.CreatedOnUtc).UtcDateTime))
            .ToList();
    }
}
