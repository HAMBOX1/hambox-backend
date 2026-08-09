using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryBySlug;

public sealed record GetCategoryBySlugQuery(string Slug) : IRequest<Result<CategoryDto>>;
