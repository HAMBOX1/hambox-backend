using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedPhones;

/// <summary>Blocks a phone number (any common format; compared by digits).</summary>
public sealed record CreateBlockedPhoneCommand(
    string PhoneNumber,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    string? IpAddress) : IRequest<Result<Guid>>;

public sealed record DeleteBlockedPhoneCommand(Guid Id) : IRequest<Result>;

public sealed record GetBlockedPhonesQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm) : IRequest<Result<PagedResult<BlockedPhoneDto>>>;

internal sealed class CreateBlockedPhoneCommandValidator : AbstractValidator<CreateBlockedPhoneCommand>
{
    public CreateBlockedPhoneCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(40)
            .Must(p => BlockedPhone.Normalize(p).Length is >= 6 and <= 20)
            .WithMessage("Enter a phone number with 6 to 20 digits.");

        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExpiresOnUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiresOnUtc.HasValue)
            .WithMessage("Expiration must be in the future.");
    }
}

internal sealed class CreateBlockedPhoneCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<CreateBlockedPhoneCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBlockedPhoneCommand request, CancellationToken cancellationToken)
    {
        var normalized = BlockedPhone.Normalize(request.PhoneNumber);
        var exists = await dbContext.BlockedPhones.AnyAsync(b => b.Pattern == normalized, cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(IdentityErrors.BlockedPhoneAlreadyExists);
        }

        var entry = BlockedPhone.Create(request.PhoneNumber, request.Reason, request.Notes, request.ExpiresOnUtc);
        dbContext.BlockedPhones.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        Guid.TryParse(currentUser.UserId, out var actorUserId);
        var lastDigits = entry.Pattern[^Math.Min(4, entry.Pattern.Length)..];
        await securityEventLogger.LogAsync(
            SecurityEventType.PhoneBlock,
            SecurityEventSeverity.Medium,
            $"Blocked phone number ending {lastDigits}: {request.Reason}",
            actorUserId == Guid.Empty ? null : actorUserId,
            ipAddress: request.IpAddress,
            cancellationToken: cancellationToken);

        return Result.Success(entry.Id);
    }
}

internal sealed class DeleteBlockedPhoneCommandHandler(
    IIdentityDbContext dbContext,
    ISecurityBlocklistService blocklistService) : IRequestHandler<DeleteBlockedPhoneCommand, Result>
{
    public async Task<Result> Handle(DeleteBlockedPhoneCommand request, CancellationToken cancellationToken)
    {
        var entry = await dbContext.BlockedPhones.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(IdentityErrors.BlockedPhoneNotFound);
        }

        dbContext.BlockedPhones.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        return Result.Success();
    }
}

internal sealed class GetBlockedPhonesQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetBlockedPhonesQuery, Result<PagedResult<BlockedPhoneDto>>>
{
    public async Task<Result<PagedResult<BlockedPhoneDto>>> Handle(
        GetBlockedPhonesQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.BlockedPhones.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = BlockedPhone.Normalize(request.SearchTerm.Trim());
            if (term.Length > 0)
            {
                query = query.Where(b => b.Pattern.Contains(term));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => new BlockedPhoneDto(b.Id, b.Pattern, b.Reason, b.Notes, b.ExpiresOnUtc, b.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BlockedPhoneDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
