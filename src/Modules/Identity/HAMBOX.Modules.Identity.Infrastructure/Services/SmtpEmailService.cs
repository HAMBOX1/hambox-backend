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
    public async Task SendEmailVerificationAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildVerificationMessage(settings, email, token, expiresAt);
        await SendAsync("EmailVerification", userId, email, message, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildPasswordResetMessage(settings, email, token, expiresAt);
        await SendAsync("PasswordReset", userId, email, message, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendAdminLoginOtpAsync(
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
        await SendAsync("AdminLoginOtp", userId, email, message, settings, cancellationToken);
    }

    private async Task SendAsync(
        string emailType,
        Guid userId,
        string email,
        MimeMessage message,
        EmailSettings settings,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
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
        }
    }

    private string? GetCorrelationId()
    {
        return httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var value) == true
            ? value?.ToString()
            : null;
    }
}
