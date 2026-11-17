namespace HAMBOX.Modules.Catalog.Application.Contracts;

public sealed record ProductFacetOptionDto(string Value, string Label, int Count, string? LabelAr = null);

public sealed record ProductFacetGroupDto(
    string Key,
    string DisplayName,
    IReadOnlyList<ProductFacetOptionDto> Options,
    string? DisplayNameAr = null,
    bool IsMultiSelect = true,
    bool DefaultExpanded = true,
    bool IsCustom = false);
