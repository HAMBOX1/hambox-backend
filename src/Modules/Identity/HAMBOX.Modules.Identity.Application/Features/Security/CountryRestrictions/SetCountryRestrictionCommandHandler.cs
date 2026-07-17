using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.CountryRestrictions;

internal sealed class SetCountryRestrictionCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<SetCountryRestrictionCommand, Result>
{
    public async Task<Result> Handle(SetCountryRestrictionCommand request, CancellationToken cancellationToken)
    {
        var code = request.CountryCode.Trim().ToUpperInvariant();
        var existing = await dbContext.CountryRestrictions.FirstOrDefaultAsync(c => c.CountryCode == code, cancellationToken);

        if (existing is null)
        {
            existing = CountryRestriction.Create(code, request.Status, request.Reason, request.Notes, request.ExpiresOnUtc);
            dbContext.CountryRestrictions.Add(existing);
        }
        else
        {
            existing.UpdateStatus(request.Status, request.Reason, request.Notes, request.ExpiresOnUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        Guid.TryParse(currentUser.UserId, out var actorUserId);
        await securityEventLogger.LogAsync(
            SecurityEventType.CountryBlock,
            request.Status == CountryRestrictionStatus.Allowed ? SecurityEventSeverity.Low : SecurityEventSeverity.Medium,
            $"Country {code} restriction set to {request.Status}: {request.Reason}",
            actorUserId == Guid.Empty ? null : actorUserId,
            ipAddress: request.IpAddress,
            country: code,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
