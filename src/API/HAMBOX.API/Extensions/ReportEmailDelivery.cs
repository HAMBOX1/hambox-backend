using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.API.Extensions;

/// <summary>Sends scheduled reports as e-mail attachments through the platform e-mail service (audit R58).</summary>
internal sealed class ReportEmailDelivery(IEmailService emailService, ILogger<ReportEmailDelivery> logger) : IReportEmailDelivery
{
    public async Task<int> SendAsync(
        IReadOnlyCollection<string> recipients,
        string subject,
        string htmlBody,
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var delivered = 0;

        foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await emailService.SendEmailWithAttachmentAsync(
                    recipient.Trim(), subject, htmlBody, fileName, content, contentType,
                    correlationId: "scheduled-report", cancellationToken: cancellationToken);
                delivered++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not e-mail scheduled report {FileName} to a recipient.", fileName);
            }
        }

        return delivered;
    }
}
