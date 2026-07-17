using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Communication.Application.Features.Preferences;

public sealed record GetCommunicationPreferencesQuery : IRequest<Result<CommunicationPreferencesDto>>;

internal sealed class GetCommunicationPreferencesQueryHandler(ICurrentUserService currentUser, ICommunicationPreferenceService preferences)
    : IRequestHandler<GetCommunicationPreferencesQuery, Result<CommunicationPreferencesDto>>
{
    public async Task<Result<CommunicationPreferencesDto>> Handle(GetCommunicationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var dto = await preferences.GetAsync(currentUser.UserId!, cancellationToken);
        return Result.Success(dto);
    }
}

public sealed record UpdateCommunicationPreferencesCommand(CommunicationPreferencesDto Preferences) : IRequest<Result>;

internal sealed class UpdateCommunicationPreferencesCommandHandler(
    ICurrentUserService currentUser,
    ICommunicationPreferenceService preferences,
    ICommunicationDbContext db)
    : IRequestHandler<UpdateCommunicationPreferencesCommand, Result>
{
    public async Task<Result> Handle(UpdateCommunicationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!;

        db.CommunicationAuditLogs.Add(CommunicationAuditLog.Create(
            CommunicationAuditAction.PreferenceUpdated, userId, "CommunicationPreference", userId));

        await preferences.UpdateAsync(userId, request.Preferences, cancellationToken);

        return Result.Success();
    }
}
