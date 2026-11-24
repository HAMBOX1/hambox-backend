using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Stars;

// Collapsed into one file like Themes' handlers: eight small request/handler pairs that share the same DTOs.

public static class StarsErrors
{
    public static readonly Error RuleNotFound = new("Stars.RuleNotFound", "The Stars rule was not found.");
    public static readonly Error UserNotFound = new("Stars.UserNotFound", "The customer was not found.");
    public static readonly Error BelowMinimumMargin = new(
        "Stars.BelowMinimumMargin", "This rule's Stars cost would push the products below the minimum margin. Lower it, or ask a staff member who can approve an override.");

    public static readonly Error InsufficientBalance = new(
        "Stars.InsufficientBalance", "The adjustment would take the customer's available Stars below zero.");
}

public sealed record StarsEarnRuleDto(
    Guid Id, string Name, string Scope, Guid? ScopeId, string Mode, decimal Value, int Priority, bool IsActive,
    DateTimeOffset? StartsOnUtc, DateTimeOffset? EndsOnUtc, string? MinRankKey = null, bool RequiresElite = false, int? MaxStarsPerOrder = null,
    string Condition = "None", decimal ConditionValue = 0m, IReadOnlyList<Guid>? ConditionProductIds = null, int? BudgetStars = null,
    int? MaxStarsPerCustomer = null, int AwardedStars = 0);

public sealed record StarsRuleInput(
    string Name, StarsRuleScope Scope, Guid? ScopeId, StarsRuleMode Mode, decimal Value, int Priority, bool IsActive,
    DateTimeOffset? StartsOnUtc, DateTimeOffset? EndsOnUtc, string? MinRankKey = null, bool RequiresElite = false, int? MaxStarsPerOrder = null,
    bool OverrideMarginGuard = false,
    StarsRuleCondition Condition = StarsRuleCondition.None,
    decimal ConditionValue = 0m,
    IReadOnlyList<Guid>? ConditionProductIds = null,
    int? BudgetStars = null,
    int? MaxStarsPerCustomer = null);

public sealed record AdminStarsAccountDto(
    string UserId, string? DisplayName, int Available, int Pending, int Reserved, int LifetimeEarned,
    PagedResult<PointsTransactionDto> Ledger);

internal static class StarsRuleMapper
{
    public static StarsEarnRuleDto ToDto(StarsEarnRule r) => new(
        r.Id, r.Name, r.Scope.ToString(), r.ScopeId, r.Mode.ToString(), r.Value, r.Priority, r.IsActive, r.StartsOnUtc, r.EndsOnUtc,
        r.MinRankKey, r.RequiresElite, r.MaxStarsPerOrder, r.Condition.ToString(), r.ConditionValue, r.RequiredProductIds(), r.BudgetStars, r.MaxStarsPerCustomer);
}

internal sealed class StarsRuleInputValidator : AbstractValidator<StarsRuleInput>
{
    public StarsRuleInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Value).InclusiveBetween(0m, 1_000_000m);
        RuleFor(x => x.Priority).InclusiveBetween(-10_000, 10_000);
        RuleFor(x => x.ScopeId).NotNull().When(x => x.Scope != StarsRuleScope.Store);
        RuleFor(x => x.ScopeId).Null().When(x => x.Scope == StarsRuleScope.Store);
        RuleFor(x => x.EndsOnUtc).GreaterThan(x => x.StartsOnUtc).When(x => x.StartsOnUtc.HasValue && x.EndsOnUtc.HasValue);
        RuleFor(x => x.MinRankKey).MaximumLength(30);
        RuleFor(x => x.MaxStarsPerOrder).InclusiveBetween(0, 100_000_000).When(x => x.MaxStarsPerOrder.HasValue);
        RuleFor(x => x.Condition).IsInEnum();
        RuleFor(x => x.ConditionValue).InclusiveBetween(0m, 100_000_000m);
        RuleFor(x => x.BudgetStars).InclusiveBetween(0, 1_000_000_000).When(x => x.BudgetStars.HasValue);
        RuleFor(x => x.MaxStarsPerCustomer).InclusiveBetween(0, 1_000_000_000).When(x => x.MaxStarsPerCustomer.HasValue);
        RuleFor(x => x.ConditionProductIds).Must(l => l is { Count: > 0 }).When(x => x.Condition == StarsRuleCondition.ContainsAllProducts)
            .WithMessage("Choose the products the order must contain.");
    }
}

// ---- Rules -------------------------------------------------------------------------------------------------------

public sealed record GetStarsRulesQuery : IRequest<Result<IReadOnlyList<StarsEarnRuleDto>>>;

internal sealed class GetStarsRulesQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetStarsRulesQuery, Result<IReadOnlyList<StarsEarnRuleDto>>>
{
    public async Task<Result<IReadOnlyList<StarsEarnRuleDto>>> Handle(GetStarsRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await db.StarsEarnRules.AsNoTracking()
            .OrderByDescending(r => r.Priority).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
        var awarded = await db.StarsRuleAwards.AsNoTracking().GroupBy(a => a.RuleId).Select(g => new { g.Key, Sum = g.Sum(a => a.Stars) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, cancellationToken);
        return Result.Success<IReadOnlyList<StarsEarnRuleDto>>(rules.Select(r => StarsRuleMapper.ToDto(r) with { AwardedStars = awarded.GetValueOrDefault(r.Id) }).ToList());
    }
}

public sealed record CreateStarsRuleCommand(StarsRuleInput Input) : IRequest<Result<StarsEarnRuleDto>>;

internal sealed class CreateStarsRuleCommandValidator : AbstractValidator<CreateStarsRuleCommand>
{
    public CreateStarsRuleCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new StarsRuleInputValidator());
}

internal sealed class CreateStarsRuleCommandHandler(ICommerceDbContext db, StarsCampaignService campaigns)
    : IRequestHandler<CreateStarsRuleCommand, Result<StarsEarnRuleDto>>
{
    public async Task<Result<StarsEarnRuleDto>> Handle(CreateStarsRuleCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        if (i.IsActive && !(await campaigns.CheckAsync(i.Scope, i.ScopeId, i.Mode, i.Value, i.OverrideMarginGuard, cancellationToken)).Allowed)
        {
            return Result.Failure<StarsEarnRuleDto>(StarsErrors.BelowMinimumMargin);
        }

        var rule = StarsEarnRule.Create(i.Name, i.Scope, i.ScopeId, i.Mode, i.Value, i.Priority, i.IsActive, i.StartsOnUtc, i.EndsOnUtc);
        rule.SetAudience(i.MinRankKey, i.RequiresElite, i.MaxStarsPerOrder);
        rule.SetCampaign(i.Condition, i.ConditionValue, i.ConditionProductIds, i.BudgetStars, i.MaxStarsPerCustomer);
        db.StarsEarnRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(StarsRuleMapper.ToDto(rule));
    }
}

public sealed record UpdateStarsRuleCommand(Guid Id, StarsRuleInput Input) : IRequest<Result<StarsEarnRuleDto>>;

internal sealed class UpdateStarsRuleCommandValidator : AbstractValidator<UpdateStarsRuleCommand>
{
    public UpdateStarsRuleCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new StarsRuleInputValidator());
}

internal sealed class UpdateStarsRuleCommandHandler(ICommerceDbContext db, StarsCampaignService campaigns)
    : IRequestHandler<UpdateStarsRuleCommand, Result<StarsEarnRuleDto>>
{
    public async Task<Result<StarsEarnRuleDto>> Handle(UpdateStarsRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.StarsEarnRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rule is null)
        {
            return Result.Failure<StarsEarnRuleDto>(StarsErrors.RuleNotFound);
        }

        var i = request.Input;
        if (i.IsActive && !(await campaigns.CheckAsync(i.Scope, i.ScopeId, i.Mode, i.Value, i.OverrideMarginGuard, cancellationToken)).Allowed)
        {
            return Result.Failure<StarsEarnRuleDto>(StarsErrors.BelowMinimumMargin);
        }

        rule.Apply(i.Name, i.Scope, i.ScopeId, i.Mode, i.Value, i.Priority, i.IsActive, i.StartsOnUtc, i.EndsOnUtc);
        rule.SetAudience(i.MinRankKey, i.RequiresElite, i.MaxStarsPerOrder);
        rule.SetCampaign(i.Condition, i.ConditionValue, i.ConditionProductIds, i.BudgetStars, i.MaxStarsPerCustomer);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(StarsRuleMapper.ToDto(rule));
    }
}

public sealed record SetStarsRuleActiveCommand(Guid Id, bool IsActive) : IRequest<Result<StarsEarnRuleDto>>;

internal sealed class SetStarsRuleActiveCommandHandler(ICommerceDbContext db, StarsCampaignService campaigns)
    : IRequestHandler<SetStarsRuleActiveCommand, Result<StarsEarnRuleDto>>
{
    public async Task<Result<StarsEarnRuleDto>> Handle(SetStarsRuleActiveCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.StarsEarnRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rule is null)
        {
            return Result.Failure<StarsEarnRuleDto>(StarsErrors.RuleNotFound);
        }

        if (request.IsActive && !rule.IsActive
            && !(await campaigns.CheckAsync(rule.Scope, rule.ScopeId, rule.Mode, rule.Value, overrideRequested: false, cancellationToken)).Allowed)
        {
            return Result.Failure<StarsEarnRuleDto>(StarsErrors.BelowMinimumMargin);
        }

        rule.SetActive(request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(StarsRuleMapper.ToDto(rule));
    }
}

public sealed record DeleteStarsRuleCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteStarsRuleCommandHandler(ICommerceDbContext db) : IRequestHandler<DeleteStarsRuleCommand, Result>
{
    public async Task<Result> Handle(DeleteStarsRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.StarsEarnRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rule is null)
        {
            return Result.Failure(StarsErrors.RuleNotFound);
        }

        db.StarsEarnRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Customer account --------------------------------------------------------------------------------------------

public sealed record GetAdminStarsAccountQuery(string UserId, int PageNumber, int PageSize, string? Type)
    : IRequest<Result<AdminStarsAccountDto>>;

internal sealed class GetAdminStarsAccountQueryHandler(
    ICommerceDbContext db, StarsLedgerService ledger, IUserDirectory users)
    : IRequestHandler<GetAdminStarsAccountQuery, Result<AdminStarsAccountDto>>
{
    public async Task<Result<AdminStarsAccountDto>> Handle(GetAdminStarsAccountQuery request, CancellationToken cancellationToken)
    {
        var names = await users.GetDisplayNamesAsync([request.UserId], cancellationToken);
        if (!names.TryGetValue(request.UserId, out var displayName))
        {
            return Result.Failure<AdminStarsAccountDto>(StarsErrors.UserNotFound);
        }

        var balance = await ledger.GetBalanceAsync(request.UserId, cancellationToken);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.PointsTransactions.AsNoTracking().Where(t => t.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.Type) && Enum.TryParse<PointsTransactionType>(request.Type, true, out var type))
        {
            query = query.Where(t => t.Type == type);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(t => t.OccurredOnUtc).ThenByDescending(t => t.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var items = rows.Select(t => new PointsTransactionDto(
            t.Id, t.Type.ToString(), t.Points, t.OrderId, t.OrderNumber, t.Description, t.OccurredOnUtc,
            t.Status.ToString(), t.AvailableOnUtc, t.ExpiresOnUtc)).ToList();

        return Result.Success(new AdminStarsAccountDto(
            request.UserId, displayName, balance.Available, balance.Pending, balance.Reserved, balance.LifetimeEarned,
            new PagedResult<PointsTransactionDto>(items, pageNumber, pageSize, total)));
    }
}

public sealed record AdjustStarsCommand(string UserId, int Points, string Reason) : IRequest<Result<AdminStarsAccountDto>>;

internal sealed class AdjustStarsCommandValidator : AbstractValidator<AdjustStarsCommand>
{
    public AdjustStarsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Points).NotEqual(0).InclusiveBetween(-1_000_000, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class AdjustStarsCommandHandler(
    StarsLedgerService ledger, IUserDirectory users, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<AdjustStarsCommand, Result<AdminStarsAccountDto>>
{
    public async Task<Result<AdminStarsAccountDto>> Handle(AdjustStarsCommand request, CancellationToken cancellationToken)
    {
        var existing = await users.GetExistingUserIdsAsync([request.UserId], cancellationToken);
        if (existing.Count == 0)
        {
            return Result.Failure<AdminStarsAccountDto>(StarsErrors.UserNotFound);
        }

        var ok = await ledger.AdjustAsync(request.UserId, request.Points, request.Reason, currentUser.UserId ?? "system", cancellationToken);
        return ok
            ? await sender.Send(new GetAdminStarsAccountQuery(request.UserId, 1, 20, null), cancellationToken)
            : Result.Failure<AdminStarsAccountDto>(StarsErrors.InsufficientBalance);
    }
}

public sealed record PreviewStarsRuleQuery(StarsRuleScope Scope, Guid? ScopeId, StarsRuleMode Mode, decimal Value) : IRequest<Result<StarsCampaignPreview>>;

internal sealed class PreviewStarsRuleQueryHandler(StarsCampaignService campaigns)
    : IRequestHandler<PreviewStarsRuleQuery, Result<StarsCampaignPreview>>
{
    public async Task<Result<StarsCampaignPreview>> Handle(PreviewStarsRuleQuery request, CancellationToken cancellationToken) =>
        Result.Success(await campaigns.PreviewAsync(request.Scope, request.ScopeId, request.Mode, request.Value, cancellationToken));
}
