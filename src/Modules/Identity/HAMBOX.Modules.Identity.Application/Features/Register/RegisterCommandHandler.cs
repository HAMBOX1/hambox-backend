using HAMBOX.Application.Referrals;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.Register;

/// <summary>
/// Handler for the <see cref="RegisterCommand"/> command.
/// </summary>
internal sealed class RegisterCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IEmailService emailService,
    ILegalDbContext legalDbContext,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger,
    IReferralRedemptionService referralRedemption,
    IOptions<EmailSettings> emailSettings) : IRequestHandler<RegisterCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await blocklistService.IsEmailBlockedAsync(request.Email, cancellationToken))
        {
            await securityEventLogger.LogAsync(
                SecurityEventType.EmailBlock,
                SecurityEventSeverity.Medium,
                $"Registration rejected for {request.Email}: email address is blocked.",
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                cancellationToken: cancellationToken);
            return Result.Failure(IdentityErrors.RegistrationNotAllowed);
        }

        var normalizedEmail = request.Email.ToUpperInvariant();
        var emailExists = await dbContext.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return Result.Failure(IdentityErrors.EmailAlreadyExists);
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);
        var user = ApplicationUser.Create(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName);

        var verificationTokenValue = tokenGenerator.GenerateSecureToken();
        var verificationToken = EmailVerificationToken.Create(
            user.Id,
            verificationTokenValue,
            DateTimeOffset.UtcNow.AddHours(24));

        dbContext.Users.Add(user);
        dbContext.EmailVerificationTokens.Add(verificationToken);

        if (!emailSettings.Value.Enabled)
        {
            user.ConfirmEmail();
            user.Activate();

            var customerRole = await dbContext.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.IsDefault, cancellationToken);

            if (customerRole is not null)
            {
                dbContext.UserRoles.Add(UserRole.Create(user.Id, customerRole.Id));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Sequential, not cross-schema-atomic with the user creation above — acceptable for a
        // best-effort side effect, not a money/inventory-critical path. Redemption itself no-ops
        // on an invalid/disabled code rather than failing, since registration has already succeeded.
        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            await referralRedemption.RedeemAsync(request.ReferralCode, user.Id.ToString(), request.Email, cancellationToken);
        }

        // Sequential, not cross-schema-atomic with the user creation above — acceptable for a
        // compliance-audit row, not a money/inventory-critical path.
        await LegalAcceptanceRecorder.RecordAsync(
            legalDbContext, user.Id.ToString(), request.IpAddress, request.UserAgent, request.Language, cancellationToken);
        await legalDbContext.SaveChangesAsync(cancellationToken);

        if (emailSettings.Value.Enabled)
        {
            await emailService.SendEmailVerificationAsync(
                user.Id,
                user.Email,
                verificationToken.ExpiresOnUtc,
                verificationTokenValue,
                cancellationToken);
        }

        return Result.Success();
    }
}
