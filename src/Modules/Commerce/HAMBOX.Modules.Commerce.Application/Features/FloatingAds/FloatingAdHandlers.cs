using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.FloatingAds;

/// <summary>An ad as the admin edits it.</summary>
public sealed record FloatingAdAdminDto(
    Guid Id,
    string TitleEn,
    string? TitleAr,
    string? MessageEn,
    string? MessageAr,
    string? ButtonLabelEn,
    string? ButtonLabelAr,
    string? LinkUrl,
    string Audience,
    bool IsActive,
    DateTimeOffset? StartsOnUtc,
    DateTimeOffset? EndsOnUtc,
    int SortOrder,
    int DismissHours);

/// <summary>The fields an admin sends when creating or updating an ad.</summary>
public sealed record FloatingAdInput(
    string TitleEn,
    string? TitleAr,
    string? MessageEn,
    string? MessageAr,
    string? ButtonLabelEn,
    string? ButtonLabelAr,
    string? LinkUrl,
    FloatingAdAudience Audience,
    bool IsActive,
    DateTimeOffset? StartsOnUtc,
    DateTimeOffset? EndsOnUtc,
    int SortOrder,
    int DismissHours);

/// <summary>An ad as the storefront shows it.</summary>
public sealed record FloatingAdPublicDto(
    Guid Id,
    string TitleEn,
    string? TitleAr,
    string? MessageEn,
    string? MessageAr,
    string? ButtonLabelEn,
    string? ButtonLabelAr,
    string? LinkUrl,
    int DismissHours);

internal static class FloatingAdErrors
{
    public static readonly Error NotFound = new("FloatingAds.NotFound", "This floating ad was not found.");

    public static readonly Error InvalidLink = new(
        "FloatingAds.InvalidLink",
        "The link must be an in-app path such as /memberships, or a full https:// address.");

    public static readonly Error InvalidDates = new(
        "FloatingAds.InvalidDates",
        "The end date must be after the start date.");

    public static readonly Error TitleRequired = new("FloatingAds.TitleRequired", "A title is required.");
}

internal static class FloatingAdMapping
{
    public static FloatingAdAdminDto ToAdminDto(FloatingAd ad) => new(
        ad.Id, ad.TitleEn, ad.TitleAr, ad.MessageEn, ad.MessageAr, ad.ButtonLabelEn, ad.ButtonLabelAr, ad.LinkUrl,
        ad.Audience.ToString(), ad.IsActive, ad.StartsOnUtc, ad.EndsOnUtc, ad.SortOrder, ad.DismissHours);

    /// <summary>Returns the first problem with the input, or null when it can be saved.</summary>
    public static Error? Validate(FloatingAdInput input)
    {
        if (string.IsNullOrWhiteSpace(input.TitleEn))
        {
            return FloatingAdErrors.TitleRequired;
        }

        if (input.StartsOnUtc is { } start && input.EndsOnUtc is { } end && end <= start)
        {
            return FloatingAdErrors.InvalidDates;
        }

        var link = input.LinkUrl?.Trim();
        if (!string.IsNullOrEmpty(link)
            && !(link.StartsWith('/') && !link.StartsWith("//", StringComparison.Ordinal))
            && !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return FloatingAdErrors.InvalidLink;
        }

        return null;
    }
}

public sealed record GetFloatingAdsAdminQuery : IRequest<Result<IReadOnlyList<FloatingAdAdminDto>>>;

internal sealed class GetFloatingAdsAdminQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetFloatingAdsAdminQuery, Result<IReadOnlyList<FloatingAdAdminDto>>>
{
    public async Task<Result<IReadOnlyList<FloatingAdAdminDto>>> Handle(
        GetFloatingAdsAdminQuery request, CancellationToken cancellationToken)
    {
        var ads = await db.FloatingAds.AsNoTracking()
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FloatingAdAdminDto>>(ads.Select(FloatingAdMapping.ToAdminDto).ToList());
    }
}

public sealed record CreateFloatingAdCommand(FloatingAdInput Input) : IRequest<Result<FloatingAdAdminDto>>;

internal sealed class CreateFloatingAdCommandHandler(ICommerceDbContext db)
    : IRequestHandler<CreateFloatingAdCommand, Result<FloatingAdAdminDto>>
{
    public async Task<Result<FloatingAdAdminDto>> Handle(CreateFloatingAdCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        if (FloatingAdMapping.Validate(input) is { } error)
        {
            return Result.Failure<FloatingAdAdminDto>(error);
        }

        var ad = FloatingAd.Create(
            input.TitleEn, input.TitleAr, input.MessageEn, input.MessageAr, input.ButtonLabelEn, input.ButtonLabelAr,
            input.LinkUrl, input.Audience, input.IsActive, input.StartsOnUtc, input.EndsOnUtc, input.SortOrder, input.DismissHours);

        db.FloatingAds.Add(ad);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(FloatingAdMapping.ToAdminDto(ad));
    }
}

public sealed record UpdateFloatingAdCommand(Guid Id, FloatingAdInput Input) : IRequest<Result<FloatingAdAdminDto>>;

internal sealed class UpdateFloatingAdCommandHandler(ICommerceDbContext db)
    : IRequestHandler<UpdateFloatingAdCommand, Result<FloatingAdAdminDto>>
{
    public async Task<Result<FloatingAdAdminDto>> Handle(UpdateFloatingAdCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        if (FloatingAdMapping.Validate(input) is { } error)
        {
            return Result.Failure<FloatingAdAdminDto>(error);
        }

        var ad = await db.FloatingAds.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (ad is null)
        {
            return Result.Failure<FloatingAdAdminDto>(FloatingAdErrors.NotFound);
        }

        ad.Update(
            input.TitleEn, input.TitleAr, input.MessageEn, input.MessageAr, input.ButtonLabelEn, input.ButtonLabelAr,
            input.LinkUrl, input.Audience, input.IsActive, input.StartsOnUtc, input.EndsOnUtc, input.SortOrder, input.DismissHours);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(FloatingAdMapping.ToAdminDto(ad));
    }
}

public sealed record SetFloatingAdActiveCommand(Guid Id, bool IsActive) : IRequest<Result>;

internal sealed class SetFloatingAdActiveCommandHandler(ICommerceDbContext db) : IRequestHandler<SetFloatingAdActiveCommand, Result>
{
    public async Task<Result> Handle(SetFloatingAdActiveCommand request, CancellationToken cancellationToken)
    {
        var ad = await db.FloatingAds.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (ad is null)
        {
            return Result.Failure(FloatingAdErrors.NotFound);
        }

        ad.SetActive(request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteFloatingAdCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteFloatingAdCommandHandler(ICommerceDbContext db) : IRequestHandler<DeleteFloatingAdCommand, Result>
{
    public async Task<Result> Handle(DeleteFloatingAdCommand request, CancellationToken cancellationToken)
    {
        var ad = await db.FloatingAds.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (ad is null)
        {
            return Result.Failure(FloatingAdErrors.NotFound);
        }

        db.FloatingAds.Remove(ad);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>The ads the current visitor should see right now (active, inside their dates, matching their audience).</summary>
public sealed record GetActiveFloatingAdsQuery : IRequest<Result<IReadOnlyList<FloatingAdPublicDto>>>;

internal sealed class GetActiveFloatingAdsQueryHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser,
    IMembershipAccessProvider membership)
    : IRequestHandler<GetActiveFloatingAdsQuery, Result<IReadOnlyList<FloatingAdPublicDto>>>
{
    public async Task<Result<IReadOnlyList<FloatingAdPublicDto>>> Handle(
        GetActiveFloatingAdsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.FloatingAds.AsNoTracking()
            .Where(a => a.IsActive
                && (a.StartsOnUtc == null || a.StartsOnUtc <= now)
                && (a.EndsOnUtc == null || a.EndsOnUtc > now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Result.Success<IReadOnlyList<FloatingAdPublicDto>>([]);
        }

        var signedIn = currentUser.IsAuthenticated && !string.IsNullOrEmpty(currentUser.UserId);
        var hasMembership = false;
        if (signedIn && candidates.Any(a => a.Audience is FloatingAdAudience.WithoutMembership or FloatingAdAudience.WithMembership))
        {
            // A fallback to the free default plan is not a membership the customer bought.
            var access = await membership.GetAccessInfoAsync(currentUser.UserId, cancellationToken);
            hasMembership = access.HasActiveMembership && access.SubscriptionId is not null;
        }

        var visible = candidates
            .Where(a => a.Audience switch
            {
                FloatingAdAudience.Everyone => true,
                FloatingAdAudience.Guests => !signedIn,
                FloatingAdAudience.SignedIn => signedIn,
                FloatingAdAudience.WithoutMembership => signedIn && !hasMembership,
                FloatingAdAudience.WithMembership => signedIn && hasMembership,
                _ => false,
            })
            .Select(a => new FloatingAdPublicDto(
                a.Id, a.TitleEn, a.TitleAr, a.MessageEn, a.MessageAr, a.ButtonLabelEn, a.ButtonLabelAr, a.LinkUrl, a.DismissHours))
            .ToList();

        return Result.Success<IReadOnlyList<FloatingAdPublicDto>>(visible);
    }
}
