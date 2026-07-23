using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProductFacets;

/// <summary>
/// Aggregates the catalog's option groups (Platform, Edition, ...) into global facets: one
/// section per group, one entry per distinct value, with a product count reflecting the
/// currently applied search/category/attribute filters.
/// </summary>
public sealed record GetProductFacetsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? AttributeFilters) : IRequest<Result<IReadOnlyList<ProductFacetGroupDto>>>;
