using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Register;

/// <summary>
/// Handler for the <see cref="RegisterCommand"/> command.
/// </summary>
internal sealed class RegisterCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IEmailService emailService) : IRequestHandler<RegisterCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
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

        await dbContext.SaveChangesAsync(cancellationToken);

        await emailService.SendEmailVerificationAsync(
            user.Id,
            user.Email,
            verificationToken.ExpiresOnUtc,
            verificationTokenValue,
            cancellationToken);

        return Result.Success();
    }
}
