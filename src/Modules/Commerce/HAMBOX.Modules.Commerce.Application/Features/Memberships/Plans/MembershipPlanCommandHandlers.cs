using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Plans;

public sealed record GetMembershipPlanByIdQuery(Guid Id) : IRequest<Result<MembershipPlanDetailDto>>;
public sealed record CreateMembershipPlanCommand(CreateMembershipPlanRequest Request) : IRequest<Result<MembershipPlanDetailDto>>;
public sealed record UpdateMembershipPlanCommand(Guid Id, UpdateMembershipPlanRequest Request) : IRequest<Result<MembershipPlanDetailDto>>;
public sealed record DeleteMembershipPlanCommand(Guid Id) : IRequest<Result>;
public sealed record DuplicateMembershipPlanCommand(Guid Id, string? NewName, string? NewSlug) : IRequest<Result<MembershipPlanDetailDto>>;
public sealed record ArchiveMembershipPlanCommand(Guid Id) : IRequest<Result<MembershipPlanDetailDto>>;
public sealed record GetPlanComparisonQuery() : IRequest<Result<PlanComparisonDto>>;

public sealed record ActivateMembershipPlanCommand(Guid Id) : IRequest<Result<MembershipPlanDetailDto>>;

internal sealed class MembershipPlanCommandHandlers :
    IRequestHandler<GetMembershipPlanByIdQuery, Result<MembershipPlanDetailDto>>,
    IRequestHandler<CreateMembershipPlanCommand, Result<MembershipPlanDetailDto>>,
    IRequestHandler<UpdateMembershipPlanCommand, Result<MembershipPlanDetailDto>>,
    IRequestHandler<DeleteMembershipPlanCommand, Result>,
    IRequestHandler<DuplicateMembershipPlanCommand, Result<MembershipPlanDetailDto>>,
    IRequestHandler<ArchiveMembershipPlanCommand, Result<MembershipPlanDetailDto>>,
    IRequestHandler<ActivateMembershipPlanCommand, Result<MembershipPlanDetailDto>>,
    IRequestHandler<GetPlanComparisonQuery, Result<PlanComparisonDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public MembershipPlanCommandHandlers(ICommerceDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<MembershipPlanDetailDto>> Handle(GetMembershipPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(request.Id, cancellationToken);
        return plan is null
            ? Result.Failure<MembershipPlanDetailDto>(CommerceErrors.MembershipPlanNotFound)
            : Result.Success(MembershipMapper.ToDetailDto(plan));
    }

    public async Task<Result<MembershipPlanDetailDto>> Handle(CreateMembershipPlanCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (await _dbContext.MembershipPlans.AnyAsync(p => p.Slug == request.Slug.Trim().ToLowerInvariant(), cancellationToken))
        {
            return Result.Failure<MembershipPlanDetailDto>(CommerceErrors.MembershipPlanSlugExists);
        }

        var plan = MembershipPlan.Create(request.Name, request.Slug, request.Description, request.Price,
            request.DurationDays, request.SortOrder, request.BadgeLabel, request.ThemeKey, request.IsDefault);
        plan.ReplaceBenefits(MembershipMapper.ParseBenefits(request.Benefits));
        if (request.IsDefault)
        {
            await ClearDefaultPlanAsync(cancellationToken);
            plan.SetDefault(true);
        }

        _dbContext.MembershipPlans.Add(plan);
        MembershipAuditWriter.Write(_dbContext, plan.Id, null, MembershipAuditAction.PlanCreated, _currentUser.UserId);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MembershipMapper.ToDetailDto(plan));
    }

    public async Task<Result<MembershipPlanDetailDto>> Handle(UpdateMembershipPlanCommand command, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(command.Id, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipPlanDetailDto>(CommerceErrors.MembershipPlanNotFound);
        }

        var request = command.Request;
        plan.Update(request.Name, request.Slug, request.Description, request.Price, request.DurationDays,
            request.SortOrder, request.BadgeLabel, request.ThemeKey);
        plan.ReplaceBenefits(MembershipMapper.ParseBenefits(request.Benefits));
        MembershipAuditWriter.Write(_dbContext, plan.Id, null, MembershipAuditAction.BenefitChanged, _currentUser.UserId, "Plan updated.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MembershipMapper.ToDetailDto(plan));
    }

    public async Task<Result> Handle(DeleteMembershipPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (plan is null)
        {
            return Result.Failure(CommerceErrors.MembershipPlanNotFound);
        }

        MembershipAuditWriter.Write(_dbContext, plan.Id, null, MembershipAuditAction.PlanDeleted, _currentUser.UserId);
        _dbContext.MembershipPlans.Remove(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<MembershipPlanDetailDto>> Handle(DuplicateMembershipPlanCommand request, CancellationToken cancellationToken)
    {
        var source = await LoadPlanAsync(request.Id, cancellationToken);
        if (source is null)
        {
            return Result.Failure<MembershipPlanDetailDto>(CommerceErrors.MembershipPlanNotFound);
        }

        var copy = source.Duplicate(request.NewName ?? $"{source.Name} (Copy)", request.NewSlug ?? $"{source.Slug}-copy");
        _dbContext.MembershipPlans.Add(copy);
        MembershipAuditWriter.Write(_dbContext, copy.Id, null, MembershipAuditAction.PlanCreated, _currentUser.UserId, $"Duplicated from {source.Id}");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MembershipMapper.ToDetailDto(copy));
    }

    public async Task<Result<MembershipPlanDetailDto>> Handle(ArchiveMembershipPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(request.Id, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipPlanDetailDto>(CommerceErrors.MembershipPlanNotFound);
        }

        plan.Archive();
        MembershipAuditWriter.Write(_dbContext, plan.Id, null, MembershipAuditAction.PlanUpdated, _currentUser.UserId, "Archived");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MembershipMapper.ToDetailDto(plan));
    }

    public async Task<Result<MembershipPlanDetailDto>> Handle(ActivateMembershipPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(request.Id, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipPlanDetailDto>(CommerceErrors.MembershipPlanNotFound);
        }

        plan.Activate();
        MembershipAuditWriter.Write(_dbContext, plan.Id, null, MembershipAuditAction.PlanUpdated, _currentUser.UserId, "Activated");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(MembershipMapper.ToDetailDto(plan));
    }

    public async Task<Result<PlanComparisonDto>> Handle(GetPlanComparisonQuery request, CancellationToken cancellationToken)
    {
        var plans = await _dbContext.MembershipPlans.Include(p => p.Benefits)
            .Where(p => p.Status == MembershipPlanStatus.Active)
            .OrderBy(p => p.SortOrder).ToListAsync(cancellationToken);
        return Result.Success(new PlanComparisonDto(plans.Select(MembershipMapper.ToDetailDto).ToList()));
    }

    private async Task<MembershipPlan?> LoadPlanAsync(Guid id, CancellationToken cancellationToken) =>
        await _dbContext.MembershipPlans.Include(p => p.Benefits).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    private async Task ClearDefaultPlanAsync(CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.MembershipPlans.Where(p => p.IsDefault).ToListAsync(cancellationToken);
        foreach (var plan in defaults)
        {
            plan.SetDefault(false);
        }
    }
}
