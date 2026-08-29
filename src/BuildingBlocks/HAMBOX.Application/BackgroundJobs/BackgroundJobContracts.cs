namespace HAMBOX.Application.BackgroundJobs;

/// <summary>
/// The one contract every background-processing engine implements. Business modules (Orders,
/// Notifications, Suppliers, Reports, ...) enqueue work through this interface only — they never
/// reference Hangfire, Quartz, RabbitMQ, or any other scheduler. Today it's backed by an in-process
/// DB-polling worker (Commerce.Infrastructure); swapping the engine later means re-registering this
/// interface in DI, nothing else.
/// </summary>
public interface IBackgroundJobScheduler
{
    Task<Guid> EnqueueAsync<TPayload>(
        string jobType,
        TPayload payload,
        BackgroundJobOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<Guid> EnqueueAsync(
        string jobType,
        BackgroundJobOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lets a module declare a recurring job at startup (e.g. "run InventorySync every 15 minutes")
/// without hardcoding the schedule into whatever worker happens to execute it.
/// </summary>
public interface IRecurringJobScheduler
{
    void Register(RecurringJobDefinition definition);
}

/// <summary>
/// Read side of the recurring-job store <see cref="IRecurringJobScheduler.Register"/> writes into —
/// used by the default worker to know what to enqueue and by the admin "Recurring Jobs" screen to
/// show what's registered. Implemented by the same singleton that implements <see cref="IRecurringJobScheduler"/>.
/// </summary>
public interface IRecurringJobRegistry
{
    IReadOnlyCollection<RecurringJobDefinition> GetAll();
}

/// <summary>
/// Non-generic marker so <see cref="IBackgroundJobHandlerRegistry"/> can hold a heterogeneous
/// collection of DI-registered handlers and resolve one by <see cref="JobType"/>.
/// </summary>
public interface IBackgroundJobHandler
{
    string JobType { get; }

    /// <summary>
    /// Invoked by the engine, which only has the raw stored payload — deserialization into
    /// <c>TPayload</c> happens inside the handler (see <see cref="BackgroundJobHandlerBase{TPayload}"/>).
    /// </summary>
    Task ExecuteRawAsync(string? payloadJson, IBackgroundJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// The contract every background-job handler implements — one per <see cref="JobType"/>. Register
/// one DI line per handler (e.g. <c>services.AddScoped&lt;IBackgroundJobHandler, SyncInventoryJobHandler&gt;()</c>);
/// nothing else in the framework needs to change to add a new job type. Inherit
/// <see cref="BackgroundJobHandlerBase{TPayload}"/> rather than implementing this directly — it wires
/// up <see cref="IBackgroundJobHandler.ExecuteRawAsync"/> for you.
/// </summary>
public interface IBackgroundJobHandler<TPayload> : IBackgroundJobHandler
{
    Task HandleAsync(TPayload payload, IBackgroundJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for job handlers — deserializes the stored payload via <see cref="IBackgroundJobSerializer"/>
/// and dispatches to the strongly-typed <see cref="HandleAsync"/>, so individual handlers never touch
/// JSON directly. Use <c>TPayload = string?</c> for jobs with no structured payload (e.g. periodic
/// maintenance jobs) — <see cref="HandleAsync"/> then simply receives the raw stored JSON, if any.
/// </summary>
public abstract class BackgroundJobHandlerBase<TPayload>(IBackgroundJobSerializer serializer) : IBackgroundJobHandler<TPayload>
{
    public abstract string JobType { get; }

    public abstract Task HandleAsync(TPayload payload, IBackgroundJobContext context, CancellationToken cancellationToken);

    public Task ExecuteRawAsync(string? payloadJson, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var payload = typeof(TPayload) == typeof(string)
            ? (TPayload)(object?)payloadJson!
            : (payloadJson is null ? default! : serializer.Deserialize<TPayload>(payloadJson)!);

        return HandleAsync(payload, context, cancellationToken);
    }
}

/// <summary>
/// Resolves the <see cref="IBackgroundJobHandler"/> registered for a given job type. The Infrastructure
/// implementation is a lookup over DI-registered instances keyed by <see cref="IBackgroundJobHandler.JobType"/> —
/// the same lookup-by-key shape as Suppliers' <c>ISupplierProviderRegistry</c>.
/// </summary>
public interface IBackgroundJobHandlerRegistry
{
    IBackgroundJobHandler? Resolve(string jobType);

    IReadOnlyCollection<string> GetRegisteredJobTypes();
}

/// <summary>
/// Everything a handler needs about the job it's currently executing. Handlers never receive the
/// storage entity itself — this is the seam that keeps handler implementations persistence-agnostic.
/// </summary>
public interface IBackgroundJobContext
{
    Guid JobId { get; }

    int Attempt { get; }

    int MaxAttempts { get; }

    string Queue { get; }

    string? CorrelationId { get; }

    string? RelatedEntityType { get; }

    string? RelatedEntityId { get; }

    Task ReportProgressAsync(int percent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin serialization seam so the scheduler and handlers agree on one payload format without every
/// caller hand-rolling <c>System.Text.Json</c> calls. Swappable if a future engine needs a different
/// wire format (e.g. a message-broker envelope).
/// </summary>
public interface IBackgroundJobSerializer
{
    string Serialize<TPayload>(TPayload payload);

    TPayload? Deserialize<TPayload>(string json);
}

/// <summary>
/// Best-effort "a job was just enqueued, consider waking up early" signal — a pure latency
/// optimization layered on top of the durable, DB-polled job queue (<see cref="IBackgroundJobScheduler"/>/
/// <c>OperationalJob</c>), never a replacement for it. The SQL row created by the scheduler remains the
/// single source of truth for whether a job exists, what state it's in, and who has claimed it — this
/// interface only shortens how long a worker waits before its next poll notices a new row.
/// </summary>
/// <remarks>
/// A missed, failed, or entirely absent notification (no implementation registered, the backing
/// transport down, a dropped message) must never lose a job — the worst case is simply that the
/// worker's next already-scheduled poll picks it up, exactly as if this interface didn't exist.
/// Implementations MUST NOT throw out of either member; swallow and log instead, so a transport outage
/// degrades this to a no-op rather than affecting job durability, enqueueing, or claiming in any way.
/// </remarks>
public interface IJobQueueNotifier
{
    /// <summary>Publishes a best-effort wake-up for <paramref name="queue"/>. Never throws.</summary>
    Task NotifyAsync(string queue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to wake-up notifications for <paramref name="queue"/>. <paramref name="onNotified"/>
    /// is invoked (fire-and-forget; the implementation is responsible for not letting an exception from
    /// it escape) whenever a notification arrives. Returns a disposable subscription — dispose it to
    /// stop listening. An implementation with no real transport (e.g. no Redis configured) returns a
    /// subscription that simply never invokes the callback, so the caller transparently falls back to
    /// its own polling interval.
    /// </summary>
    IAsyncDisposable Subscribe(string queue, Func<Task> onNotified);
}

public enum BackgroundJobPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}

public enum BackgroundJobStatus
{
    Queued = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Retrying = 4,
    Cancelled = 5,
    DeadLetter = 6,
}

/// <summary>
/// Named queues jobs can be routed to. Purely a routing label today (the default engine processes
/// all queues from one worker) — a future engine can dedicate workers per queue without any change
/// to how business modules enqueue work.
/// </summary>
public static class BackgroundJobQueues
{
    public const string Default = "default";
    public const string Emails = "emails";
    public const string Notifications = "notifications";
    public const string Suppliers = "suppliers";
    public const string Reports = "reports";
    public const string Maintenance = "maintenance";
    public const string HighPriority = "high-priority";
    public const string FuturePayment = "future-payment";
}

public sealed record BackgroundJobOptions(
    string Queue = BackgroundJobQueues.Default,
    BackgroundJobPriority Priority = BackgroundJobPriority.Normal,
    int? MaxAttempts = null,
    string? CorrelationId = null,
    string? RelatedEntityType = null,
    string? RelatedEntityId = null);

public sealed record RecurringJobDefinition(
    string Key,
    string JobType,
    TimeSpan Interval,
    string Queue = BackgroundJobQueues.Default,
    BackgroundJobPriority Priority = BackgroundJobPriority.Normal);
