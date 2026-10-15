using System.Net;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.API.Extensions;

/// <summary>
/// E-mails new critical operational alerts (stuck jobs, failed deliveries, worker down) to the addresses in
/// <c>Monitoring:AlertEmails</c> (audit R44). Sent through the same SMTP path every other e-mail uses. Never throws:
/// a mail problem must not break the job that raised the alert.
/// </summary>
internal sealed class OperationalAlertEmailNotifier(
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<OperationalAlertEmailNotifier> logger) : IOperationalAlertNotifier
{
    public async Task NotifyAsync(
        string code, string title, string message, string severity, CancellationToken cancellationToken = default)
    {
        var recipients = configuration.GetSection("Monitoring:AlertEmails").Get<string[]>()
            ?.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (recipients.Length == 0)
        {
            logger.LogWarning(
                "Operational alert {Code} raised but Monitoring:AlertEmails is empty — nobody was e-mailed.", code);
            return;
        }

        var subject = $"[HAMBOX {severity}] {title}";
        var body = $"<p><strong>{WebUtility.HtmlEncode(title)}</strong></p><p>{WebUtility.HtmlEncode(message)}</p>" +
                   $"<p>Code: {WebUtility.HtmlEncode(code)}</p>";

        foreach (var recipient in recipients)
        {
            try
            {
                await emailService.SendTemplatedEmailAsync(recipient, subject, body, $"alert-{code}", cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not e-mail operational alert {Code} to {Recipient}.", code, recipient);
            }
        }
    }
}
