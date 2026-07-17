using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs;

internal sealed class OperationalJobExecutionContext(OperationalJob job, ICommerceDbContext db) : IBackgroundJobContext
{
    public Guid JobId => job.Id;
    public int Attempt => job.Attempts;
    public int MaxAttempts => job.MaxAttempts;
    public string Queue => job.Queue;
    public string? CorrelationId => job.CorrelationId;
    public string? RelatedEntityType => job.RelatedEntityType;
    public string? RelatedEntityId => job.RelatedEntityId;

    public async Task ReportProgressAsync(int percent, CancellationToken cancellationToken = default)
    {
        job.SetProgress(percent);
        await db.SaveChangesAsync(cancellationToken);
    }
}
