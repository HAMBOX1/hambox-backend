using HAMBOX.Modules.Identity.Application.Abstractions;
using MaxMind.Db;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Resolves country/city from a local MaxMind GeoLite2-City database. Registered instead of
/// <see cref="NullCountryResolver"/> when <c>GeoIp:DatabasePath</c> points at an existing
/// <c>.mmdb</c> file (see <see cref="Extensions.IdentityInfrastructureExtensions"/>) — falls back
/// to returning null for addresses the database has no entry for (private/reserved IPs, unknown
/// ranges) rather than throwing.
/// </summary>
internal sealed class MaxMindGeoLocationResolver(DatabaseReader databaseReader) : ICountryResolver
{
    public Task<GeoLocationResult?> ResolveCountryAsync(CountryResolutionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IpAddress))
        {
            return Task.FromResult<GeoLocationResult?>(null);
        }

        try
        {
            if (databaseReader.TryCity(request.IpAddress, out var response))
            {
                return Task.FromResult<GeoLocationResult?>(new GeoLocationResult(
                    response.Country.IsoCode,
                    response.Country.Name,
                    response.City.Name));
            }
        }
        catch (AddressNotFoundException)
        {
            // Private/reserved/unroutable IP (e.g. local dev via loopback) — no entry, not an error.
        }
        catch (InvalidDatabaseException)
        {
            // Corrupt/incompatible .mmdb file — degrade to "unknown" rather than fail the request.
        }

        return Task.FromResult<GeoLocationResult?>(null);
    }
}
