using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.GetMe;

/// <summary>
/// Query to retrieve the authenticated user's profile.
/// </summary>
public sealed record GetMeQuery() : IRequest<Result<UserProfileDto>>;
