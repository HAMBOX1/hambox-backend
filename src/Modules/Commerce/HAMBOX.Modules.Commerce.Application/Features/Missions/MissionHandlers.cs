using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Missions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Missions;

// Missions and milestones (EX-01). Collapsed into one file like the other loyalty handler sets.

public static class MissionErrors
{
    public static readonly Error NotFound = new("Missions.NotFound", "The mission was not found.");
}

public sealed record AdminMissionDto(
    Guid Id, string NameEn, string NameAr, string? DescriptionEn, string? DescriptionAr, string Kind, decimal Target, int RewardStars,
    string? BadgeKey, bool IsActive, DateTimeOffset? StartsOnUtc, DateTimeOffset? EndsOnUtc, int SortOrder, int Completions);

public sealed record MissionInput(
    string NameEn, string NameAr, string? DescriptionEn, string? DescriptionAr, MissionKind Kind, decimal Target, int RewardStars,
    string? BadgeKey, bool IsActive, DateTimeOffset? StartsOnUtc, DateTimeOffset? EndsOnUtc, int SortOrder);

internal sealed class MissionInputValidator : AbstractValidator<MissionInput>
{
    public MissionInputValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DescriptionEn).MaximumLength(500);
        RuleFor(x => x.DescriptionAr).MaximumLength(500);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Target).InclusiveBetween(1m, 100_000_000m);
        RuleFor(x => x.RewardStars).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.BadgeKey).MaximumLength(60);
        RuleFor(x => x.EndsOnUtc).GreaterThan(x => x.StartsOnUtc).When(x => x.StartsOnUtc.HasValue && x.EndsOnUtc.HasValue);
    }
}

public sealed record GetMyMissionsQuery : IRequest<Result<IReadOnlyList<MissionProgressDto>>>;

internal sealed class GetMyMissionsQueryHandler(MissionService missions, ICurrentUserService currentUser)
    : IRequestHandler<GetMyMissionsQuery, Result<IReadOnlyList<MissionProgressDto>>>
{
    public async Task<Result<IReadOnlyList<MissionProgressDto>>> Handle(GetMyMissionsQuery request, CancellationToken cancellationToken) =>
        currentUser.IsAuthenticated && currentUser.UserId is { } userId
            ? Result.Success(await missions.GetProgressAsync(userId, cancellationToken))
            : Result.Failure<IReadOnlyList<MissionProgressDto>>(CommerceErrors.AuthenticationRequired);
}

public sealed record GetAdminMissionsQuery : IRequest<Result<IReadOnlyList<AdminMissionDto>>>;

internal sealed class GetAdminMissionsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetAdminMissionsQuery, Result<IReadOnlyList<AdminMissionDto>>>
{
    public async Task<Result<IReadOnlyList<AdminMissionDto>>> Handle(GetAdminMissionsQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Missions.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var counts = await db.MissionCompletions.AsNoTracking().GroupBy(c => c.MissionId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return Result.Success<IReadOnlyList<AdminMissionDto>>(rows.Select(m => new AdminMissionDto(
            m.Id, m.NameEn, m.NameAr, m.DescriptionEn, m.DescriptionAr, m.Kind.ToString(), m.Target, m.RewardStars, m.BadgeKey, m.IsActive,
            m.StartsOnUtc, m.EndsOnUtc, m.SortOrder, counts.GetValueOrDefault(m.Id))).ToList());
    }
}

public sealed record SaveMissionCommand(Guid? Id, MissionInput Input) : IRequest<Result<Guid>>;

internal sealed class SaveMissionCommandValidator : AbstractValidator<SaveMissionCommand>
{
    public SaveMissionCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new MissionInputValidator());
}

internal sealed class SaveMissionCommandHandler(ICommerceDbContext db) : IRequestHandler<SaveMissionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveMissionCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        Mission mission;
        if (request.Id is { } id)
        {
            var existing = await db.Missions.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<Guid>(MissionErrors.NotFound);
            }

            mission = existing;
            mission.Apply(i.NameEn, i.NameAr, i.DescriptionEn, i.DescriptionAr, i.Kind, i.Target, i.RewardStars, i.BadgeKey, i.IsActive, i.StartsOnUtc, i.EndsOnUtc, i.SortOrder);
        }
        else
        {
            mission = Mission.Create(i.NameEn, i.NameAr, i.DescriptionEn, i.DescriptionAr, i.Kind, i.Target, i.RewardStars, i.BadgeKey, i.IsActive, i.StartsOnUtc, i.EndsOnUtc, i.SortOrder);
            db.Missions.Add(mission);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(mission.Id);
    }
}

public sealed record DeleteMissionCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteMissionCommandHandler(ICommerceDbContext db) : IRequestHandler<DeleteMissionCommand, Result>
{
    public async Task<Result> Handle(DeleteMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
        if (mission is null)
        {
            return Result.Failure(MissionErrors.NotFound);
        }

        db.Missions.Remove(mission);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
