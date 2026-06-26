using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.UpdateProfile;

/// <summary>
/// Command to update the authenticated user's profile information.
/// </summary>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="PhoneNumber">The user's phone number.</param>
public sealed record UpdateProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? PreferredLanguage,
    string? PreferredCurrency) : IRequest<Result<UserProfileDto>>;
