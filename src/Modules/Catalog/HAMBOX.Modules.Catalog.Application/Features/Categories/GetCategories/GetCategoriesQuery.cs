using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategories;

public record GetCategoriesQuery(int PageNumber, int PageSize, string? SearchTerm, bool? ActiveOnly) : IRequest<Result<PagedResult<CategoryDto>>>;
