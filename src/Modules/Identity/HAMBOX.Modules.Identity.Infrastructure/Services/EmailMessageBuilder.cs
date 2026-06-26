using System.Net.Mail;
using HAMBOX.Modules.Identity.Application.Options;
using MimeKit;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Builds MIME messages for identity transactional emails.
/// </summary>
internal static class EmailMessageBuilder
{
    /// <summary>
    /// Builds an email verification message.
    /// </summary>
    public static MimeMessage BuildVerificationMessage(
        EmailSettings settings,
        string recipientEmail,
        string token,
        DateTimeOffset expiresAtUtc)
    {
        var actionUrl = BuildActionUrl(settings.ApplicationBaseUrl, settings.VerificationPath, token);
        return BuildMessage(
            settings,
            recipientEmail,
            "Verify your HAMBOX account",
            "Verify your email address",
            "Please verify your email address by clicking the link below:",
            actionUrl,
            expiresAtUtc);
    }

    /// <summary>
    /// Builds a password reset message.
    /// </summary>
    public static MimeMessage BuildPasswordResetMessage(
        EmailSettings settings,
        string recipientEmail,
        string token,
        DateTimeOffset expiresAtUtc)
    {
        var actionUrl = BuildActionUrl(settings.ApplicationBaseUrl, settings.ResetPasswordPath, token);
        return BuildMessage(
            settings,
            recipientEmail,
            "Reset your HAMBOX password",
            "Reset your password",
            "You requested a password reset. Click the link below to choose a new password:",
            actionUrl,
            expiresAtUtc);
    }

    private static MimeMessage BuildMessage(
        EmailSettings settings,
        string recipientEmail,
        string subject,
        string heading,
        string intro,
        string actionUrl,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionUrl);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;

        var expiryText = expiresAtUtc.ToString("u");
        var plainText = $"""
            {heading}

            {intro}

            {actionUrl}

            This link expires on {expiryText} (UTC).

            If you did not request this email, you can safely ignore it.
            """;

        var html = $"""
            <p>{heading}</p>
            <p>{intro}</p>
            <p><a href="{actionUrl}">{actionUrl}</a></p>
            <p>This link expires on <strong>{expiryText}</strong> (UTC).</p>
            <p>If you did not request this email, you can safely ignore it.</p>
            """;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = plainText,
            HtmlBody = html
        };

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private static string BuildActionUrl(string baseUrl, string path, string token)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{normalizedBase}{normalizedPath}?token={Uri.EscapeDataString(token)}";
    }
}
