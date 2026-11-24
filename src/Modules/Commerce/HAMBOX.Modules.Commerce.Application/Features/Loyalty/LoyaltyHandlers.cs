using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Loyalty;
using HAMBOX.Modules.Commerce.Domain.Loyalty;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Loyalty;

// Collapsed into one file like Stars' and Themes' handlers: small request/handler pairs sharing the same DTOs.

public static class LoyaltyErrors
{
    public static readonly Error RankNotFound = new("Loyalty.RankNotFound", "The loyalty rank was not found.");
    public static readonly Error UserNotFound = new("Loyalty.UserNotFound", "The customer was not found.");
}

public sealed record LoyaltyRankHistoryDto(string? From, string To, string Reason, string? ActorUserId, DateTimeOffset OccurredOnUtc);

public sealed record AdminLoyaltyCustomerDto(string UserId, string? DisplayName, LoyaltyStatusDto Status, IReadOnlyList<LoyaltyRankHistoryDto> History);

public sealed record UpdateLoyaltyRankInput(
    string NameEn, string NameAr, int SortOrder, decimal Threshold, bool IsVisible, string? AccentColor);

// ---- Customer ----------------------------------------------------------------------------------------------------

public sealed record GetMyLoyaltyQuery : IRequest<Result<LoyaltyStatusDto>>;

internal sealed class GetMyLoyaltyQueryHandler(LoyaltyRankService ranks, ICurrentUserService currentUser)
    : IRequestHandler<GetMyLoyaltyQuery, Result<LoyaltyStatusDto>>
{
    public async Task<Result<LoyaltyStatusDto>> Handle(GetMyLoyaltyQuery request, CancellationToken cancellationToken) =>
        currentUser.IsAuthenticated && currentUser.UserId is { } userId
            ? Result.Success(await ranks.GetStatusAsync(userId, cancellationToken))
            : Result.Failure<LoyaltyStatusDto>(CommerceErrors.AuthenticationRequired);
}

// ---- Admin -------------------------------------------------------------------------------------------------------

public sealed record GetLoyaltyRanksQuery : IRequest<Result<IReadOnlyList<LoyaltyRankDto>>>;

internal sealed class GetLoyaltyRanksQueryHandler(LoyaltyRankService ranks)
    : IRequestHandler<GetLoyaltyRanksQuery, Result<IReadOnlyList<LoyaltyRankDto>>>
{
    public async Task<Result<IReadOnlyList<LoyaltyRankDto>>> Handle(GetLoyaltyRanksQuery request, CancellationToken cancellationToken) =>
        Result.Success<IReadOnlyList<LoyaltyRankDto>>((await ranks.EnsureRanksAsync(cancellationToken)).Select(LoyaltyRankService.ToDto).ToList());
}

public sealed record UpdateLoyaltyRankCommand(Guid Id, UpdateLoyaltyRankInput Input) : IRequest<Result<LoyaltyRankDto>>;

internal sealed class UpdateLoyaltyRankCommandValidator : AbstractValidator<UpdateLoyaltyRankCommand>
{
    public UpdateLoyaltyRankCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.NameEn).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Input.NameAr).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Input.SortOrder).InclusiveBetween(0, 100);
        RuleFor(x => x.Input.Threshold).InclusiveBetween(0m, 100_000_000m);
        RuleFor(x => x.Input.AccentColor).Matches("^#[0-9a-fA-F]{3,8}$").When(x => !string.IsNullOrEmpty(x.Input.AccentColor));
    }
}

internal sealed class UpdateLoyaltyRankCommandHandler(ICommerceDbContext db)
    : IRequestHandler<UpdateLoyaltyRankCommand, Result<LoyaltyRankDto>>
{
    public async Task<Result<LoyaltyRankDto>> Handle(UpdateLoyaltyRankCommand request, CancellationToken cancellationToken)
    {
        var rank = await db.LoyaltyRanks.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rank is null)
        {
            return Result.Failure<LoyaltyRankDto>(LoyaltyErrors.RankNotFound);
        }

        var i = request.Input;
        rank.Update(i.NameEn, i.NameAr, i.SortOrder, i.Threshold, i.IsVisible, i.AccentColor);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(LoyaltyRankService.ToDto(rank));
    }
}

public sealed record RecalculateLoyaltyCommand : IRequest<Result<int>>;

internal sealed class RecalculateLoyaltyCommandHandler(LoyaltyRankService ranks) : IRequestHandler<RecalculateLoyaltyCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RecalculateLoyaltyCommand request, CancellationToken cancellationToken) =>
        Result.Success(await ranks.RecalculateAllAsync(cancellationToken));
}

public sealed record GetCustomerLoyaltyQuery(string UserId) : IRequest<Result<AdminLoyaltyCustomerDto>>;

internal sealed class GetCustomerLoyaltyQueryHandler(LoyaltyRankService ranks, ICommerceDbContext db, IUserDirectory users)
    : IRequestHandler<GetCustomerLoyaltyQuery, Result<AdminLoyaltyCustomerDto>>
{
    public async Task<Result<AdminLoyaltyCustomerDto>> Handle(GetCustomerLoyaltyQuery request, CancellationToken cancellationToken)
    {
        var names = await users.GetDisplayNamesAsync([request.UserId], cancellationToken);
        if (!names.TryGetValue(request.UserId, out var name))
        {
            return Result.Failure<AdminLoyaltyCustomerDto>(LoyaltyErrors.UserNotFound);
        }

        var status = await ranks.GetStatusAsync(request.UserId, cancellationToken);
        var history = await db.LoyaltyRankHistory.AsNoTracking()
            .Where(h => h.UserId == request.UserId)
            .OrderByDescending(h => h.OccurredOnUtc)
            .Take(50)
            .Select(h => new LoyaltyRankHistoryDto(h.FromRankKey, h.ToRankKey, h.Reason, h.ActorUserId, h.OccurredOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminLoyaltyCustomerDto(request.UserId, name, status, history));
    }
}

/// <summary>Pins <see cref="RankKey"/> for a customer with a recorded reason; a null key releases the pin.</summary>
public sealed record SetCustomerRankCommand(string UserId, string? RankKey, string Reason) : IRequest<Result<AdminLoyaltyCustomerDto>>;

internal sealed class SetCustomerRankCommandValidator : AbstractValidator<SetCustomerRankCommand>
{
    public SetCustomerRankCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class SetCustomerRankCommandHandler(
    LoyaltyRankService ranks, IUserDirectory users, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<SetCustomerRankCommand, Result<AdminLoyaltyCustomerDto>>
{
    public async Task<Result<AdminLoyaltyCustomerDto>> Handle(SetCustomerRankCommand request, CancellationToken cancellationToken)
    {
        if ((await users.GetExistingUserIdsAsync([request.UserId], cancellationToken)).Count == 0)
        {
            return Result.Failure<AdminLoyaltyCustomerDto>(LoyaltyErrors.UserNotFound);
        }

        var ok = await ranks.SetManualAsync(request.UserId, request.RankKey, request.Reason.Trim(), currentUser.UserId ?? "system", cancellationToken);
        return ok
            ? await sender.Send(new GetCustomerLoyaltyQuery(request.UserId), cancellationToken)
            : Result.Failure<AdminLoyaltyCustomerDto>(LoyaltyErrors.RankNotFound);
    }
}
