using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.CountryRestrictions;

/// <summary>
/// Returns the full ISO-3166 country list merged with any administrator override. When
/// <paramref name="OverriddenOnly"/> is true, only countries with an explicit override row are
/// returned (the admin-facing "managed list" view rather than the full ~250-country picker).
/// </summary>
public sealed record GetCountryRestrictionsQuery(
    string? SearchTerm,
    bool OverriddenOnly) : IRequest<Result<IReadOnlyCollection<CountryRestrictionDto>>>;
