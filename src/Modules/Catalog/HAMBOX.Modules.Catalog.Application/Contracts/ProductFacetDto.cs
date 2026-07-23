namespace HAMBOX.Modules.Catalog.Application.Contracts;

public sealed record ProductFacetOptionDto(string Value, string Label, int Count);

public sealed record ProductFacetGroupDto(string Key, string DisplayName, IReadOnlyList<ProductFacetOptionDto> Options);
