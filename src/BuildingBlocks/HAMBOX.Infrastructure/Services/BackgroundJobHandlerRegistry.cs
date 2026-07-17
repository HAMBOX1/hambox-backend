using HAMBOX.Application.BackgroundJobs;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// Resolves by <see cref="IBackgroundJobHandler.JobType"/> over whatever <see cref="IBackgroundJobHandler"/>
/// instances are registered in DI — the same lookup-by-key shape as Suppliers' <c>SupplierProviderRegistry</c>.
/// Adding a new job type is exactly: implement <see cref="IBackgroundJobHandler{TPayload}"/>, register it
/// with <c>services.AddScoped&lt;IBackgroundJobHandler, X&gt;()</c> in the owning module's Infrastructure
/// extension. This class never changes.
/// </summary>
internal sealed class BackgroundJobHandlerRegistry(IEnumerable<IBackgroundJobHandler> handlers) : IBackgroundJobHandlerRegistry
{
    public IBackgroundJobHandler? Resolve(string jobType) =>
        handlers.FirstOrDefault(h => string.Equals(h.JobType, jobType, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<string> GetRegisteredJobTypes() =>
        handlers.Select(h => h.JobType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
