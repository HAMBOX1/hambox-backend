using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Default <see cref="ICountryResolver"/>: always returns null. Country blocking's CRUD,
/// middleware enforcement, and audit logging are fully implemented, but have no effect until this
/// is swapped for a real resolver — see <see cref="ICountryResolver"/> for the extension point.
/// </summary>
internal sealed class NullCountryResolver : ICountryResolver
{
    public Task<string?> ResolveCountryAsync(CountryResolutionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
