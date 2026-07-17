using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Logs transactional email requests without sending when SMTP is disabled.
/// </summary>
internal sealed class LoggingEmailService(
    IOptions<EmailSettings> emailSettings,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment,
    ILogger<LoggingEmailService> logger) : IEmailService
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
        LogEmail("EmailVerification", userId, email, expiresAt, token, _settings.VerificationPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        LogEmail("PasswordReset", userId, email, expiresAt, token, _settings.ResetPasswordPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAdminLoginOtpAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        LogOtp("AdminLoginOtp", userId, email, expiresAt, code);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendTemplatedEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email delivery disabled. Templated email '{Subject}' queued for {MaskedEmail}. CorrelationId={CorrelationId}",
            subject,
            EmailLogHelper.MaskEmail(toEmail),
            correlationId);
        return Task.CompletedTask;
    }

    private void LogOtp(
        string emailType,
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string secret)
    {
        var correlationId = httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var value) == true
            ? value?.ToString()
            : null;

        logger.LogInformation(
            "Email delivery disabled. {EmailType} queued for user {UserId} at {MaskedEmail}. ExpiresAtUtc={ExpiresAtUtc}. CorrelationId={CorrelationId}",
            emailType,
            userId,
            EmailLogHelper.MaskEmail(email),
            expiresAt,
            correlationId);

        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "Development OTP for {EmailType}, user {UserId}: {Secret}. Use the code from the most recent sign-in or resend. CorrelationId={CorrelationId}",
                emailType,
                userId,
                secret,
                correlationId);
            return;
        }

        logger.LogDebug(
            "Email fallback secret for {EmailType}, user {UserId}: {Secret}. CorrelationId={CorrelationId}",
            emailType,
            userId,
            secret,
            correlationId);
    }

    private void LogEmail(
        string emailType,
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        string path)
    {
        var correlationId = httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var value) == true
            ? value?.ToString()
            : null;

        var actionUrl = $"{_settings.ApplicationBaseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(token)}";

        logger.LogInformation(
            "Email delivery disabled. {EmailType} queued for user {UserId} at {MaskedEmail}. ExpiresAtUtc={ExpiresAtUtc}. CorrelationId={CorrelationId}",
            emailType,
            userId,
            EmailLogHelper.MaskEmail(email),
            expiresAt,
            correlationId);

        logger.LogDebug(
            "Email fallback URL for {EmailType}, user {UserId}: {ActionUrl}. CorrelationId={CorrelationId}",
            emailType,
            userId,
            actionUrl,
            correlationId);
    }
}
