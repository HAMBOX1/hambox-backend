using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

internal sealed class DeleteBlockedEmailCommandHandler(
    IIdentityDbContext dbContext,
    ISecurityBlocklistService blocklistService) : IRequestHandler<DeleteBlockedEmailCommand, Result>
{
    public async Task<Result> Handle(DeleteBlockedEmailCommand request, CancellationToken cancellationToken)
    {
        var entry = await dbContext.BlockedEmails.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(IdentityErrors.BlockedEmailNotFound);
        }

        dbContext.BlockedEmails.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        return Result.Success();
    }
}
