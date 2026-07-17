using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Contracts;
using HAMBOX.Modules.Communication.Application.Errors;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Application.Features.Providers;

public sealed record GetCommunicationProvidersQuery : IRequest<Result<IReadOnlyList<CommunicationProviderConfigDto>>>;

internal sealed class GetCommunicationProvidersQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetCommunicationProvidersQuery, Result<IReadOnlyList<CommunicationProviderConfigDto>>>
{
    public async Task<Result<IReadOnlyList<CommunicationProviderConfigDto>>> Handle(GetCommunicationProvidersQuery request, CancellationToken cancellationToken)
    {
        var items = await db.CommunicationProviderConfigs.AsNoTracking()
            .OrderBy(p => p.Priority)
            .Select(p => new CommunicationProviderConfigDto(p.Id, p.Name, p.ProviderType, p.Channel, p.Priority, p.IsEnabled))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CommunicationProviderConfigDto>>(items);
    }
}

public sealed record UpdateCommunicationProviderCommand(Guid Id, UpdateCommunicationProviderRequest Request) : IRequest<Result>;

internal sealed class UpdateCommunicationProviderCommandHandler(ICommunicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateCommunicationProviderCommand, Result>
{
    public async Task<Result> Handle(UpdateCommunicationProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await db.CommunicationProviderConfigs.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (provider is null)
        {
            return Result.Failure(CommunicationErrors.ProviderNotFound);
        }

        var req = request.Request;
        if (req.IsEnabled)
        {
            provider.Enable();
        }
        else
        {
            provider.Disable();
        }

        provider.UpdatePriority(req.Priority);

        db.CommunicationAuditLogs.Add(CommunicationAuditLog.Create(
            req.IsEnabled ? CommunicationAuditAction.ProviderEnabled : CommunicationAuditAction.ProviderDisabled,
            currentUser.UserId, "CommunicationProviderConfig", provider.Id.ToString()));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
