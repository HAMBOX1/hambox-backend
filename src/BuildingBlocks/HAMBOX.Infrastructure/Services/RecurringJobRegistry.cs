using System.Collections.Concurrent;
using HAMBOX.Application.BackgroundJobs;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// In-memory store of recurring-job definitions, keyed by <see cref="RecurringJobDefinition.Key"/> so
/// re-registering the same key (e.g. on app restart) replaces rather than duplicates. Registered as a
/// singleton implementing both <see cref="IRecurringJobScheduler"/> (write, business-facing) and
/// <see cref="IRecurringJobRegistry"/> (read, used by the default worker + admin UI).
/// </summary>
internal sealed class RecurringJobRegistry : IRecurringJobScheduler, IRecurringJobRegistry
{
    private readonly ConcurrentDictionary<string, RecurringJobDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);

    public void Register(RecurringJobDefinition definition) => definitions[definition.Key] = definition;

    public IReadOnlyCollection<RecurringJobDefinition> GetAll() => definitions.Values.ToArray();
}
