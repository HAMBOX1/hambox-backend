using System.Security.Cryptography;
using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Codes;

// Gift Pass / Trial / Offer codes (EL-07). Collapsed into one file like the other loyalty handler sets.

public static class MembershipCodeErrors
{
    public static readonly Error Disabled = new("MembershipCode.Disabled", "Membership codes are not available right now.");
    public static readonly Error Invalid = new("MembershipCode.Invalid", "This code is not valid, has expired or has been fully used.");
    public static readonly Error AlreadyRedeemed = new("MembershipCode.AlreadyRedeemed", "You have already used this code.");
    public static readonly Error PlanUnavailable = new("MembershipCode.PlanUnavailable", "The plan for this code is no longer available.");
    public static readonly Error CodeExists = new("MembershipCode.Exists", "A code with this value already exists.");
}

public sealed record MembershipCodeDto(
    Guid Id, string Code, string Kind, Guid PlanId, string PlanName, int? GrantDays, int MaxRedemptions, int RedeemedCount,
    DateTimeOffset? ExpiresOnUtc, bool IsActive, string? Note);

public sealed record MembershipCodeRedemptionResultDto(string PlanName, DateTime StartsOnUtc, DateTime ExpiresOnUtc);

public sealed record CreateMembershipCodeInput(
    string? Code, MembershipCodeKind Kind, Guid PlanId, int? GrantDays, int MaxRedemptions, DateTimeOffset? ExpiresOnUtc, string? Note);

// ---- Customer ----------------------------------------------------------------------------------------------------

public sealed record RedeemMembershipCodeCommand(string Code) : IRequest<Result<MembershipCodeRedemptionResultDto>>;

internal sealed class RedeemMembershipCodeCommandValidator : AbstractValidator<RedeemMembershipCodeCommand>
{
    public RedeemMembershipCodeCommandValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
}

internal sealed class RedeemMembershipCodeCommandHandler(
    ICommerceDbContext db, ICurrentUserService currentUser, IPlatformSettingsProvider settings)
    : IRequestHandler<RedeemMembershipCodeCommand, Result<MembershipCodeRedemptionResultDto>>
{
    public async Task<Result<MembershipCodeRedemptionResultDto>> Handle(
        RedeemMembershipCodeCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<MembershipCodeRedemptionResultDto>(CommerceErrors.AuthenticationRequired);
        }

        var features = await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        if (!features.EliteSubscription || !features.GiftPass)
        {
            return Result.Failure<MembershipCodeRedemptionResultDto>(MembershipCodeErrors.Disabled);
        }

        var normalized = request.Code.Trim().ToUpperInvariant();
        var code = await db.MembershipCodes.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (code is null || !code.CanRedeemAt(now))
        {
            return Result.Failure<MembershipCodeRedemptionResultDto>(MembershipCodeErrors.Invalid);
        }

        if (await db.MembershipCodeRedemptions.AnyAsync(r => r.CodeId == code.Id && r.UserId == userId, cancellationToken))
        {
            return Result.Failure<MembershipCodeRedemptionResultDto>(MembershipCodeErrors.AlreadyRedeemed);
        }

        var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == code.PlanId && p.Status == MembershipPlanStatus.Active, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipCodeRedemptionResultDto>(MembershipCodeErrors.PlanUnavailable);
        }

        // Claim a use atomically so two customers racing for the last redemption can't both win.
        var claimed = await db.MembershipCodes
            .Where(c => c.Id == code.Id && c.RedeemedCount < c.MaxRedemptions)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RedeemedCount, c => c.RedeemedCount + 1), cancellationToken);
        if (claimed == 0)
        {
            return Result.Failure<MembershipCodeRedemptionResultDto>(MembershipCodeErrors.Invalid);
        }

        var days = code.GrantDays ?? plan.DurationDays;
        var months = code.GrantDays is null ? plan.DurationMonths : 0;
        var active = await db.MembershipSubscriptions
            .Where(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active)
            .ToListAsync(cancellationToken);

        var sameLive = active.FirstOrDefault(s => s.PlanId == plan.Id && s.IsActiveAt(DateTime.UtcNow));
        MembershipSubscription subscription;
        if (sameLive is not null)
        {
            sameLive.Renew(days, months);
            subscription = sameLive;
        }
        else
        {
            foreach (var other in active)
            {
                other.Cancel();
            }

            subscription = MembershipSubscription.Create(userId, plan.Id, days, autoRenew: false, months);
            db.MembershipSubscriptions.Add(subscription);
        }

        db.MembershipHistories.Add(MembershipHistory.Create(
            subscription.Id, userId, plan.Id, MembershipHistoryAction.Assigned, notes: $"Redeemed {code.Kind} code."));
        db.MembershipCodeRedemptions.Add(MembershipCodeRedemption.Create(code.Id, userId, subscription.Id));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique (code, user) index: a concurrent double submit. The use claimed above is returned.
            await db.MembershipCodes.Where(c => c.Id == code.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.RedeemedCount, c => c.RedeemedCount - 1), cancellationToken);
            return Result.Failure<MembershipCodeRedemptionResultDto>(MembershipCodeErrors.AlreadyRedeemed);
        }

        return Result.Success(new MembershipCodeRedemptionResultDto(plan.Name, subscription.StartsOnUtc, subscription.ExpiresOnUtc));
    }
}

// ---- Admin -------------------------------------------------------------------------------------------------------

public sealed record GetMembershipCodesQuery : IRequest<Result<IReadOnlyList<MembershipCodeDto>>>;

internal sealed class GetMembershipCodesQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetMembershipCodesQuery, Result<IReadOnlyList<MembershipCodeDto>>>
{
    public async Task<Result<IReadOnlyList<MembershipCodeDto>>> Handle(GetMembershipCodesQuery request, CancellationToken cancellationToken)
    {
        var rows = await (from c in db.MembershipCodes.AsNoTracking()
                          join p in db.MembershipPlans.AsNoTracking() on c.PlanId equals p.Id
                          orderby c.CreatedOnUtc descending
                          select new MembershipCodeDto(
                              c.Id, c.Code, c.Kind.ToString(), c.PlanId, p.Name, c.GrantDays, c.MaxRedemptions, c.RedeemedCount,
                              c.ExpiresOnUtc, c.IsActive, c.Note))
            .Take(500)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<MembershipCodeDto>>(rows);
    }
}

public sealed record CreateMembershipCodeCommand(CreateMembershipCodeInput Input) : IRequest<Result<MembershipCodeDto>>;

internal sealed class CreateMembershipCodeCommandValidator : AbstractValidator<CreateMembershipCodeCommand>
{
    public CreateMembershipCodeCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Code).MaximumLength(64).Matches("^[A-Za-z0-9-]*$").When(x => !string.IsNullOrEmpty(x.Input.Code));
        RuleFor(x => x.Input.Kind).IsInEnum();
        RuleFor(x => x.Input.MaxRedemptions).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.Input.GrantDays).InclusiveBetween(1, 3650).When(x => x.Input.GrantDays.HasValue);
        RuleFor(x => x.Input.Note).MaximumLength(500);
    }
}

internal sealed class CreateMembershipCodeCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateMembershipCodeCommand, Result<MembershipCodeDto>>
{
    public async Task<Result<MembershipCodeDto>> Handle(CreateMembershipCodeCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        var plan = await db.MembershipPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == i.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipCodeDto>(CommerceErrors.MembershipPlanNotFound);
        }

        var value = string.IsNullOrWhiteSpace(i.Code) ? Generate() : i.Code.Trim().ToUpperInvariant();
        if (await db.MembershipCodes.AnyAsync(c => c.Code == value, cancellationToken))
        {
            return Result.Failure<MembershipCodeDto>(MembershipCodeErrors.CodeExists);
        }

        var code = MembershipCode.Create(value, i.Kind, i.PlanId, i.GrantDays, i.MaxRedemptions, i.ExpiresOnUtc, i.Note, currentUser.UserId);
        db.MembershipCodes.Add(code);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(code, plan.Name));
    }

    private static string Generate()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(12, 0, (span, _) =>
        {
            for (var n = 0; n < span.Length; n++)
            {
                span[n] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }
        });
    }

    internal static MembershipCodeDto ToDto(MembershipCode c, string planName) =>
        new(c.Id, c.Code, c.Kind.ToString(), c.PlanId, planName, c.GrantDays, c.MaxRedemptions, c.RedeemedCount, c.ExpiresOnUtc, c.IsActive, c.Note);
}

public sealed record SetMembershipCodeActiveCommand(Guid Id, bool IsActive) : IRequest<Result>;

internal sealed class SetMembershipCodeActiveCommandHandler(ICommerceDbContext db) : IRequestHandler<SetMembershipCodeActiveCommand, Result>
{
    public async Task<Result> Handle(SetMembershipCodeActiveCommand request, CancellationToken cancellationToken)
    {
        var code = await db.MembershipCodes.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (code is null)
        {
            return Result.Failure(MembershipCodeErrors.Invalid);
        }

        code.SetActive(request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
