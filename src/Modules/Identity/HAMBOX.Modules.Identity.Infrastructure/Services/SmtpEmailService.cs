using System.Diagnostics;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// </summary>
internal sealed class SmtpEmailService(
    IPlatformSettingsService platformSettings,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    /// <inheritdoc />
    public async Task<bool> SendEmailVerificationAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildVerificationMessage(settings, email, token, expiresAt);
        return await SendAsync("EmailVerification", userId, email, message, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildPasswordResetMessage(settings, email, token, expiresAt);
        return await SendAsync("PasswordReset", userId, email, message, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendAdminLoginOtpAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "Development OTP for AdminLoginOtp, user {UserId} at {MaskedEmail}: {Code}. Use the code from the most recent sign-in or resend.",
                userId,
                EmailLogHelper.MaskEmail(email),
                code);
        }

        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildAdminOtpMessage(settings, email, code, expiresAt);
        return await SendAsync("AdminLoginOtp", userId, email, message, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendTemplatedEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildTemplatedMessage(settings, toEmail, subject, htmlBody);
        var sent = await SendCoreAsync("Templated", null, toEmail, message, settings, correlationId, cancellationToken);

        if (!sent)
        {
            throw new InvalidOperationException($"Failed to send templated email to {EmailLogHelper.MaskEmail(toEmail)}.");
        }
    }

    private async Task<bool> SendAsync(
        string emailType,
        Guid userId,
        string email,
        MimeMessage message,
        EmailSettings settings,
        CancellationToken cancellationToken) =>
        await SendCoreAsync(emailType, userId, email, message, settings, null, cancellationToken);

    /// <summary>Returns <see langword="true"/> on a successful send; never throws — callers that need to
    /// surface delivery failure (e.g. the Communication Platform's background job retry) check the result.</summary>
    private async Task<bool> SendCoreAsync(
        string emailType,
        Guid? userId,
        string email,
        MimeMessage message,
        EmailSettings settings,
        string? correlationIdOverride,
        CancellationToken cancellationToken)
    {
        var correlationId = correlationIdOverride ?? GetCorrelationId();
        var maskedEmail = EmailLogHelper.MaskEmail(email);
        var stopwatch = Stopwatch.StartNew();

        logger.LogDebug(
            "Sending {EmailType} email to {MaskedEmail} for user {UserId} via {SmtpHost}:{SmtpPort}",
            emailType,
            maskedEmail,
            userId,
            settings.SmtpHost,
            settings.SmtpPort);

        try
        {
            using var client = new SmtpClient
            {
                Timeout = 30_000
            };

            var socketOptions = settings.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            stopwatch.Stop();
            logger.LogInformation(
                "Sent {EmailType} email to {MaskedEmail} for user {UserId} in {ElapsedMs}ms. CorrelationId={CorrelationId}",
                emailType,
                maskedEmail,
                userId,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Failed to send {EmailType} email to {MaskedEmail} for user {UserId} via {SmtpHost}:{SmtpPort} after {ElapsedMs}ms. CorrelationId={CorrelationId}",
                emailType,
                maskedEmail,
                userId,
                settings.SmtpHost,
                settings.SmtpPort,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            return false;
        }
    }

    private string? GetCorrelationId()
    {
        return httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var value) == true
            ? value?.ToString()
            : null;
    }
}
