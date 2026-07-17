using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class PlatformRoutingEmailService(
    IPlatformSettingsService platformSettings,
    SmtpEmailService smtpEmailService,
    LoggingEmailService loggingEmailService) : IEmailService
{
    public async Task SendEmailVerificationAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var service = await ResolveAsync(cancellationToken);
        await service.SendEmailVerificationAsync(userId, email, expiresAt, token, cancellationToken);
    }

    public async Task SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var service = await ResolveAsync(cancellationToken);
        await service.SendPasswordResetAsync(userId, email, expiresAt, token, cancellationToken);
    }

    public async Task SendAdminLoginOtpAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var service = await ResolveAsync(cancellationToken);
        await service.SendAdminLoginOtpAsync(userId, email, code, expiresAt, cancellationToken);
    }

    public async Task SendTemplatedEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var service = await ResolveAsync(cancellationToken);
        await service.SendTemplatedEmailAsync(toEmail, subject, htmlBody, correlationId, cancellationToken);
    }

    private async Task<IEmailService> ResolveAsync(CancellationToken cancellationToken)
    {
        var email = await platformSettings.GetEmailAsync(cancellationToken);
        return email.Enabled ? smtpEmailService : loggingEmailService;
    }
}
