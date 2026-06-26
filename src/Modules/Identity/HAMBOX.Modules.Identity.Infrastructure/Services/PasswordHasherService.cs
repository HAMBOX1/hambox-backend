using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service to hash and verify passwords using ASP.NET Core Identity's PasswordHasher.
/// </summary>
internal sealed class PasswordHasherService(IPasswordHasher<ApplicationUser> passwordHasher) : IPasswordHasher
{
    /// <inheritdoc />
    public string HashPassword(string password)
    {
        return passwordHasher.HashPassword(default!, password);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = passwordHasher.VerifyHashedPassword(default!, hashedPassword, providedPassword);
        return result != PasswordVerificationResult.Failed;
    }
}
