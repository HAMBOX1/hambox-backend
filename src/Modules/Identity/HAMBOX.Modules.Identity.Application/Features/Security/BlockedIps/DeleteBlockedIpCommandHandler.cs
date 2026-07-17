using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

internal sealed class DeleteBlockedIpCommandHandler(
    IIdentityDbContext dbContext,
    ISecurityBlocklistService blocklistService) : IRequestHandler<DeleteBlockedIpCommand, Result>
{
    public async Task<Result> Handle(DeleteBlockedIpCommand request, CancellationToken cancellationToken)
    {
        var entry = await dbContext.BlockedIps.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(IdentityErrors.BlockedIpNotFound);
        }

        dbContext.BlockedIps.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        return Result.Success();
    }
}
