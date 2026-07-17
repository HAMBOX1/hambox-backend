using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.CountryRestrictions;

/// <summary>
/// Sets (or clears, via <see cref="CountryRestrictionStatus.Allowed"/>) the access override for
/// one ISO-3166 alpha-2 country code.
/// </summary>
public sealed record SetCountryRestrictionCommand(
    string CountryCode,
    CountryRestrictionStatus Status,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    string? IpAddress) : IRequest<Result>;
