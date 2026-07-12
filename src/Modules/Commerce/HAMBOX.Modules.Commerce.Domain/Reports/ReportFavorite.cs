namespace HAMBOX.Modules.Commerce.Domain.Reports;

public sealed class ReportFavorite
{
    private ReportFavorite()
    {
    }

    private ReportFavorite(Guid id, string userId, Guid reportDefinitionId)
    {
        Id = id;
        UserId = userId;
        ReportDefinitionId = reportDefinitionId;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid ReportDefinitionId { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }

    public static ReportFavorite Create(string userId, Guid reportDefinitionId) =>
        new(Guid.NewGuid(), userId, reportDefinitionId);
}
