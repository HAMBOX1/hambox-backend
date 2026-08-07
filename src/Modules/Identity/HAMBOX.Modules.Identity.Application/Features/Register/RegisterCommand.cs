using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Register;

/// <summary>
/// Command to register a new user in the system.
/// </summary>
/// <param name="Email">The email address of the user.</param>
/// <param name="Password">The password of the user.</param>
/// <param name="FirstName">The first name of the user.</param>
/// <param name="LastName">The last name of the user.</param>
/// <param name="IpAddress">The client IP address, used to record legal acceptance.</param>
/// <param name="UserAgent">The client user agent, used to record legal acceptance.</param>
/// <param name="Language">The client's preferred language, used to record legal acceptance.</param>
/// <param name="ReferralCode">Optional referral code the registrant was invited with.</param>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string IpAddress,
    string UserAgent,
    string Language,
    string? ReferralCode = null) : IRequest<Result>;
