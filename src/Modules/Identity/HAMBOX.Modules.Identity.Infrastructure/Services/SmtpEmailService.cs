using System.Diagnostics;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// </summary>
internal sealed class SmtpEmailService(
    IOptions<EmailSettings> emailSettings,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = emailSettings.Value;

    /// <inheritdoc />
    public Task SendEmailVerificationAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var message = EmailMessageBuilder.BuildVerificationMessage(_settings, email, token, expiresAt);
        return SendAsync("EmailVerification", userId, email, message, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var message = EmailMessageBuilder.BuildPasswordResetMessage(_settings, email, token, expiresAt);
        return SendAsync("PasswordReset", userId, email, message, cancellationToken);
    }

    private async Task SendAsync(
        string emailType,
        Guid userId,
        string email,
        MimeMessage message,
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
            _settings.SmtpHost,
            _settings.SmtpPort);

        try
        {
            using var client = new SmtpClient
            {
                Timeout = 30_000
            };

            var socketOptions = _settings.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password ?? string.Empty, cancellationToken);
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
                _settings.SmtpHost,
                _settings.SmtpPort,
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
