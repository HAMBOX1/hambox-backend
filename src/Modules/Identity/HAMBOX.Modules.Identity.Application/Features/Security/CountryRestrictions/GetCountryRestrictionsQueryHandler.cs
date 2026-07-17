using System.Globalization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.CountryRestrictions;

internal sealed class GetCountryRestrictionsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetCountryRestrictionsQuery, Result<IReadOnlyCollection<CountryRestrictionDto>>>
{
    public async Task<Result<IReadOnlyCollection<CountryRestrictionDto>>> Handle(
        GetCountryRestrictionsQuery request,
        CancellationToken cancellationToken)
    {
        var overrides = await dbContext.CountryRestrictions
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CountryCode, cancellationToken);

        IEnumerable<CountryRestrictionDto> items;

        if (request.OverriddenOnly)
        {
            items = overrides.Values.Select(o => new CountryRestrictionDto(
                o.CountryCode,
                GetCountryName(o.CountryCode),
                o.Status.ToString(),
                o.Reason,
                o.Notes,
                o.ExpiresOnUtc));
        }
        else
        {
            items = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Select(culture =>
                {
                    try
                    {
                        return new RegionInfo(culture.Name);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                })
                .Where(region => region is not null)
                .DistinctBy(region => region!.TwoLetterISORegionName)
                .OrderBy(region => region!.EnglishName)
                .Select(region =>
                {
                    overrides.TryGetValue(region!.TwoLetterISORegionName, out var restriction);
                    return new CountryRestrictionDto(
                        region.TwoLetterISORegionName,
                        region.EnglishName,
                        (restriction?.Status ?? CountryRestrictionStatus.Allowed).ToString(),
                        restriction?.Reason,
                        restriction?.Notes,
                        restriction?.ExpiresOnUtc);
                });
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            items = items.Where(c =>
                c.CountryName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.CountryCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return Result.Success<IReadOnlyCollection<CountryRestrictionDto>>(items.ToList());
    }

    private static string GetCountryName(string countryCode)
    {
        try
        {
            return new RegionInfo(countryCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryCode;
        }
    }
}
