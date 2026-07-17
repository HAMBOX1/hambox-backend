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

    /// <summary>
    /// Builds an admin portal login OTP message.
    /// </summary>
    public static MimeMessage BuildAdminOtpMessage(
        EmailSettings settings,
        string recipientEmail,
        string code,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "HAMBOX Admin Portal verification code";

        var expiryText = expiresAtUtc.ToString("u");
        var plainText = $"""
            Admin Portal sign-in verification

            Your one-time verification code is: {code}

            This code expires on {expiryText} (UTC).

            If you did not attempt to sign in, secure your account immediately.
            """;

        var html = $"""
            <p><strong>Admin Portal sign-in verification</strong></p>
            <p>Your one-time verification code is:</p>
            <p style="font-size:24px;letter-spacing:4px;"><strong>{code}</strong></p>
            <p>This code expires on <strong>{expiryText}</strong> (UTC).</p>
            <p>If you did not attempt to sign in, secure your account immediately.</p>
            """;

        message.Body = new BodyBuilder { TextBody = plainText, HtmlBody = html }.ToMessageBody();
        return message;
    }

    /// <summary>
    /// Builds a message from an already-rendered subject/HTML body — used by the Communication
    /// Platform's <c>EmailCommunicationProvider</c>, where the subject/body come from a
    /// <c>CommunicationTemplate</c> rather than one of the hardcoded identity flows above.
    /// </summary>
    public static MimeMessage BuildTemplatedMessage(EmailSettings settings, string recipientEmail, string subject, string htmlBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();
        return message;
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
