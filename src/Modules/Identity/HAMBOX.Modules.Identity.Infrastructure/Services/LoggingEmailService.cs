using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Logs transactional email requests without sending when SMTP is disabled.
/// </summary>
internal sealed class LoggingEmailService(
    IOptions<EmailSettings> emailSettings,
    IHttpContextAccessor httpContextAccessor,
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
