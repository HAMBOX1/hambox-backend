namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Renders localized email templates for outbound notifications.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders a named email template using the current UI culture.
    /// </summary>
  /// <param name="templateName">Template identifier (e.g. VerifyEmail).</param>
  /// <param name="model">Template model values.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Localized subject and HTML body.</returns>
    Task<EmailTemplateContent> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Localized email content.
/// </summary>
/// <param name="Subject">Email subject line.</param>
/// <param name="HtmlBody">HTML email body.</param>
public sealed record EmailTemplateContent(string Subject, string HtmlBody);
