namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// E-mails a generated report to its recipients as an attachment (scheduled reports, audit R58). Implemented
/// in the API host on top of the platform e-mail service; implementations must not throw for one bad recipient.
/// </summary>
public interface IReportEmailDelivery
{
    /// <returns>How many recipients the report reached.</returns>
    Task<int> SendAsync(
        IReadOnlyCollection<string> recipients,
        string subject,
        string htmlBody,
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);
}
