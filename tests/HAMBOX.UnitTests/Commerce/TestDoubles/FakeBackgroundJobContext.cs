using HAMBOX.Application.BackgroundJobs;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakeBackgroundJobContext : IBackgroundJobContext
{
    public Guid JobId { get; } = Guid.NewGuid();
    public int Attempt { get; init; } = 1;
    public int MaxAttempts { get; init; } = 3;
    public string Queue { get; init; } = "default";
    public string? CorrelationId { get; init; }
    public string? RelatedEntityType { get; init; }
    public string? RelatedEntityId { get; init; }

    public Task ReportProgressAsync(int percent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
