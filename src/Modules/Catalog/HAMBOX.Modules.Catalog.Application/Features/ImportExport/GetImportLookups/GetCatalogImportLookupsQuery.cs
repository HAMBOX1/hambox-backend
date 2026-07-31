using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;

/// <summary>One selectable value for a template dropdown or a correction picker — Value is what gets written to the cell, Label is what the admin sees.</summary>
public sealed record CatalogImportLookupItem(string Value, string Label);

/// <summary>
/// Backs both the Excel template's dropdown columns and the wizard's "select existing or create
/// new" correction pickers — one source of truth for both. Suppliers and Membership Plans are
/// deliberately excluded: Catalog.Application has no established reference to
/// Suppliers.Application/Commerce.Application (only Catalog.Infrastructure does, for the Execute
/// job's actual writes), and adding one solely to populate a dropdown isn't worth the new
/// cross-module coupling — matches the existing "membership plan link is never auto-relinked,
/// reassign manually" design decision.
/// </summary>
public sealed record CatalogImportLookups(
    IReadOnlyList<CatalogImportLookupItem> Categories,
    IReadOnlyList<CatalogImportLookupItem> Collections,
    IReadOnlyList<CatalogImportLookupItem> VariantGroups,
    IReadOnlyList<string> ProductStatuses,
    IReadOnlyList<string> VariantStatuses,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<string> SkuStrategies,
    IReadOnlyList<string> DuplicateStrategies);

public sealed record GetCatalogImportLookupsQuery : IRequest<Result<CatalogImportLookups>>;
