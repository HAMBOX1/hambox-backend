using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Cosmetics;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cosmetics;

// Profile frames, badges and animations (EX-04). Collapsed into one file like the other loyalty handler sets.

public sealed record AdminCosmeticDto(
    Guid Id, string Kind, string Key, string NameEn, string NameAr, string? ImageUrl, string? AccentColor, string Unlock,
    string? UnlockRankKey, Guid? UnlockMissionId, int? StarsPrice, bool IsActive, int SortOrder);

public sealed record CosmeticInput(
    CosmeticKind Kind, string Key, string NameEn, string NameAr, string? ImageUrl, string? AccentColor, CosmeticUnlock Unlock,
    string? UnlockRankKey, Guid? UnlockMissionId, int? StarsPrice, bool IsActive, int SortOrder);

public sealed record EquipRequest(Guid? FrameId, Guid? BadgeId, Guid? AnimationId);

internal sealed class CosmeticInputValidator : AbstractValidator<CosmeticInput>
{
    public CosmeticInputValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Unlock).IsInEnum();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(60).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ImageUrl).MaximumLength(500)
            .Must(u => string.IsNullOrEmpty(u) || u.StartsWith('/') || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .WithMessage("The picture must be an uploaded file or an https:// address.");
        RuleFor(x => x.AccentColor).Matches("^#[0-9a-fA-F]{3,8}$").When(x => !string.IsNullOrEmpty(x.AccentColor));
        RuleFor(x => x.StarsPrice).InclusiveBetween(1, 100_000_000).When(x => x.StarsPrice.HasValue);
    }
}

public sealed record GetMyProfileStyleQuery : IRequest<Result<ProfileStyleDto>>;

internal sealed class GetMyProfileStyleQueryHandler(CosmeticService cosmetics, ICurrentUserService currentUser)
    : IRequestHandler<GetMyProfileStyleQuery, Result<ProfileStyleDto>>
{
    public async Task<Result<ProfileStyleDto>> Handle(GetMyProfileStyleQuery request, CancellationToken cancellationToken) =>
        currentUser.IsAuthenticated && currentUser.UserId is { } userId
            ? Result.Success(await cosmetics.GetAsync(userId, cancellationToken))
            : Result.Failure<ProfileStyleDto>(CommerceErrors.AuthenticationRequired);
}

public sealed record EquipProfileStyleCommand(Guid? FrameId, Guid? BadgeId, Guid? AnimationId) : IRequest<Result<ProfileStyleDto>>;

internal sealed class EquipProfileStyleCommandHandler(CosmeticService cosmetics, ICurrentUserService currentUser)
    : IRequestHandler<EquipProfileStyleCommand, Result<ProfileStyleDto>>
{
    public async Task<Result<ProfileStyleDto>> Handle(EquipProfileStyleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<ProfileStyleDto>(CommerceErrors.AuthenticationRequired);
        }

        return await cosmetics.EquipAsync(userId, request.FrameId, request.BadgeId, request.AnimationId, cancellationToken) is { } error
            ? Result.Failure<ProfileStyleDto>(error)
            : Result.Success(await cosmetics.GetAsync(userId, cancellationToken));
    }
}

public sealed record BuyCosmeticCommand(Guid CosmeticId) : IRequest<Result<ProfileStyleDto>>;

internal sealed class BuyCosmeticCommandHandler(CosmeticService cosmetics, ICurrentUserService currentUser)
    : IRequestHandler<BuyCosmeticCommand, Result<ProfileStyleDto>>
{
    public async Task<Result<ProfileStyleDto>> Handle(BuyCosmeticCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<ProfileStyleDto>(CommerceErrors.AuthenticationRequired);
        }

        return await cosmetics.BuyAsync(userId, request.CosmeticId, cancellationToken) is { } error
            ? Result.Failure<ProfileStyleDto>(error)
            : Result.Success(await cosmetics.GetAsync(userId, cancellationToken));
    }
}

public sealed record GetAdminCosmeticsQuery : IRequest<Result<IReadOnlyList<AdminCosmeticDto>>>;

internal sealed class GetAdminCosmeticsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetAdminCosmeticsQuery, Result<IReadOnlyList<AdminCosmeticDto>>>
{
    public async Task<Result<IReadOnlyList<AdminCosmeticDto>>> Handle(GetAdminCosmeticsQuery request, CancellationToken cancellationToken) =>
        Result.Success<IReadOnlyList<AdminCosmeticDto>>((await db.ProfileCosmetics.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(cancellationToken))
            .Select(c => new AdminCosmeticDto(
                c.Id, c.Kind.ToString(), c.Key, c.NameEn, c.NameAr, c.ImageUrl, c.AccentColor, c.Unlock.ToString(), c.UnlockRankKey,
                c.UnlockMissionId, c.StarsPrice, c.IsActive, c.SortOrder)).ToList());
}

public sealed record SaveCosmeticCommand(Guid? Id, CosmeticInput Input) : IRequest<Result<Guid>>;

internal sealed class SaveCosmeticCommandValidator : AbstractValidator<SaveCosmeticCommand>
{
    public SaveCosmeticCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new CosmeticInputValidator());
}

internal sealed class SaveCosmeticCommandHandler(ICommerceDbContext db) : IRequestHandler<SaveCosmeticCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveCosmeticCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        var key = i.Key.Trim().ToLowerInvariant();
        if (await db.ProfileCosmetics.AnyAsync(c => c.Key == key && c.Id != request.Id, cancellationToken))
        {
            return Result.Failure<Guid>(new HAMBOX.SharedKernel.Errors.Error("Cosmetics.KeyExists", "Another item already uses this key."));
        }

        ProfileCosmetic item;
        if (request.Id is { } id)
        {
            var existing = await db.ProfileCosmetics.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<Guid>(CosmeticErrors.NotFound);
            }

            item = existing;
            item.Apply(i.Kind, i.Key, i.NameEn, i.NameAr, i.ImageUrl, i.AccentColor, i.Unlock, i.UnlockRankKey, i.UnlockMissionId, i.StarsPrice, i.IsActive, i.SortOrder);
        }
        else
        {
            item = ProfileCosmetic.Create(i.Kind, i.Key, i.NameEn, i.NameAr, i.ImageUrl, i.AccentColor, i.Unlock, i.UnlockRankKey, i.UnlockMissionId, i.StarsPrice, i.IsActive, i.SortOrder);
            db.ProfileCosmetics.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(item.Id);
    }
}

public sealed record DeleteCosmeticCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteCosmeticCommandHandler(ICommerceDbContext db) : IRequestHandler<DeleteCosmeticCommand, Result>
{
    public async Task<Result> Handle(DeleteCosmeticCommand request, CancellationToken cancellationToken)
    {
        var item = await db.ProfileCosmetics.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (item is null)
        {
            return Result.Failure(CosmeticErrors.NotFound);
        }

        db.ProfileCosmetics.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
