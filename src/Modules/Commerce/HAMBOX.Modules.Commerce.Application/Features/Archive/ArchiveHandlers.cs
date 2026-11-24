using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Archive;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Archive;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Archive;

// Old-archive import and linking (CU-03). Collapsed into one file like the other loyalty handler sets.

public static class ArchiveErrors
{
    public static readonly Error Unreadable = new("Archive.Unreadable", "The file could not be read. Upload an .xlsx or .csv with a header row.");
    public static readonly Error Empty = new("Archive.Empty", "The file has no customers or orders to import.");
    public static readonly Error NotFound = new("Archive.NotFound", "The archive record was not found, or it is already linked.");
    public static readonly Error UserNotFound = new("Archive.UserNotFound", "The customer account was not found.");
}

public sealed record ArchiveRecordDto(
    Guid Id, string LegacyId, string? Email, string? Phone, string? Name, int PromisedStars, string Status, string? LinkedUserId,
    DateTimeOffset? LinkedOnUtc, DateTimeOffset ImportedOnUtc);

public sealed record ImportArchiveCommand(Stream Content, string FileName, bool Commit) : IRequest<Result<ArchiveImportReport>>;

internal sealed class ImportArchiveCommandHandler(ILegacyArchiveParser parser, LegacyArchiveService archive)
    : IRequestHandler<ImportArchiveCommand, Result<ArchiveImportReport>>
{
    public async Task<Result<ArchiveImportReport>> Handle(ImportArchiveCommand request, CancellationToken cancellationToken)
    {
        ParsedArchive parsed;
        try
        {
            parsed = parser.Parse(request.Content, request.FileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<ArchiveImportReport>(ArchiveErrors.Unreadable);
        }

        if (parsed.Customers.Count == 0 && parsed.Orders.Count == 0)
        {
            return Result.Failure<ArchiveImportReport>(ArchiveErrors.Empty);
        }

        return Result.Success(await archive.ImportAsync(parsed, request.Commit, cancellationToken));
    }
}

public sealed record ListArchiveRecordsQuery(string? Search, string? Status, int PageNumber, int PageSize)
    : IRequest<Result<PagedResult<ArchiveRecordDto>>>;

internal sealed class ListArchiveRecordsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<ListArchiveRecordsQuery, Result<PagedResult<ArchiveRecordDto>>>
{
    public async Task<Result<PagedResult<ArchiveRecordDto>>> Handle(ListArchiveRecordsQuery request, CancellationToken cancellationToken)
    {
        var query = db.LegacyCustomerRecords.AsNoTracking().AsQueryable();
        if (Enum.TryParse<LegacyLinkStatus>(request.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.LegacyId.Contains(term) || (r.Email != null && r.Email.Contains(term)) || (r.Name != null && r.Name.Contains(term)));
        }

        var page = Math.Max(request.PageNumber, 1);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.ImportedOnUtc).Skip((page - 1) * size).Take(size)
            .Select(r => new ArchiveRecordDto(r.Id, r.LegacyId, r.Email, r.Phone, r.Name, r.PromisedStars, r.Status.ToString(), r.LinkedUserId, r.LinkedOnUtc, r.ImportedOnUtc))
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<ArchiveRecordDto>(rows, page, size, total));
    }
}

public sealed record LinkArchiveRecordCommand(Guid RecordId, string UserId, string Reason) : IRequest<Result<ArchiveClaimResult>>;

internal sealed class LinkArchiveRecordCommandValidator : AbstractValidator<LinkArchiveRecordCommand>
{
    public LinkArchiveRecordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class LinkArchiveRecordCommandHandler(LegacyArchiveService archive, IUserDirectory users, ICurrentUserService currentUser)
    : IRequestHandler<LinkArchiveRecordCommand, Result<ArchiveClaimResult>>
{
    public async Task<Result<ArchiveClaimResult>> Handle(LinkArchiveRecordCommand request, CancellationToken cancellationToken)
    {
        if ((await users.GetExistingUserIdsAsync([request.UserId], cancellationToken)).Count == 0)
        {
            return Result.Failure<ArchiveClaimResult>(ArchiveErrors.UserNotFound);
        }

        var result = await archive.LinkManuallyAsync(request.RecordId, request.UserId, currentUser.UserId ?? "system", request.Reason.Trim(), cancellationToken);
        return result.Linked == 0 ? Result.Failure<ArchiveClaimResult>(ArchiveErrors.NotFound) : Result.Success(result);
    }
}

/// <summary>A signed-in customer claims the archive records that match their verified e-mail.</summary>
public sealed record ClaimArchiveCommand : IRequest<Result<ArchiveClaimResult>>;

internal sealed class ClaimArchiveCommandHandler(LegacyArchiveService archive, ICurrentUserService currentUser)
    : IRequestHandler<ClaimArchiveCommand, Result<ArchiveClaimResult>>
{
    public async Task<Result<ArchiveClaimResult>> Handle(ClaimArchiveCommand request, CancellationToken cancellationToken) =>
        currentUser.IsAuthenticated && currentUser.UserId is { } userId
            ? Result.Success(await archive.ClaimForUserAsync(userId, cancellationToken))
            : Result.Failure<ArchiveClaimResult>(CommerceErrors.AuthenticationRequired);
}
