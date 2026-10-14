using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Security;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Library;

/// <summary>
/// Shows a purchased license key in plaintext to its owner. Depending on Platform Settings → Security →
/// <c>CustomerRevealOtpMode</c> the customer first has to enter an e-mailed code (once per key, or every time).
/// </summary>
public sealed record RevealCustomerLibraryKeyQuery(Guid LibraryItemId, string? OtpCode = null)
    : IRequest<Result<RevealCustomerLibraryKeyDto>>;

/// <summary>E-mails the customer a one-time code that unlocks revealing one specific license key.</summary>
public sealed record RequestLicenseKeyRevealOtpCommand(Guid LibraryItemId)
    : IRequest<Result<RevealOtpChallengeDto>>;

public sealed record RevealOtpChallengeDto(DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt, string MaskedEmail);

internal static class LicenseKeyRevealPolicy
{
    public const string Purpose = "LicenseKeyReveal";
    public const string EventRevealed = "LicenseKeyRevealed";

    public static bool IsOff(string? mode) => string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase);

    public static bool IsEveryReveal(string? mode) => string.Equals(mode, "EveryReveal", StringComparison.OrdinalIgnoreCase);
}

internal sealed class RevealCustomerLibraryKeyQueryHandler
    : IRequestHandler<RevealCustomerLibraryKeyQuery, Result<RevealCustomerLibraryKeyDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlatformSettingsProvider _platformSettings;
    private readonly ICustomerActionOtpService _otp;

    public RevealCustomerLibraryKeyQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService,
        IPlatformSettingsProvider platformSettings,
        ICustomerActionOtpService otp)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
        _platformSettings = platformSettings;
        _otp = otp;
    }

    public async Task<Result<RevealCustomerLibraryKeyDto>> Handle(
        RevealCustomerLibraryKeyQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.AuthenticationRequired);
        }

        var userId = _currentUserService.UserId;

        var key = await (
            from license in _commerceDbContext.OrderLicenseKeys.AsNoTracking()
            join order in _commerceDbContext.Orders.AsNoTracking() on license.OrderId equals order.Id
            where license.Id == request.LibraryItemId
                  && order.UserId == userId
            select license)
            .FirstOrDefaultAsync(cancellationToken);

        if (key is null || string.IsNullOrWhiteSpace(key.LicenseKey))
        {
            return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.OrderLicenseKeyNotFound);
        }

        var security = await _platformSettings.GetSecurityAsync(cancellationToken);
        var mode = security.CustomerRevealOtpMode;

        if (!LicenseKeyRevealPolicy.IsOff(mode))
        {
            var keyMarker = key.Id.ToString();
            var revealedBefore = !LicenseKeyRevealPolicy.IsEveryReveal(mode)
                && await _commerceDbContext.OrderAuditEntries.AsNoTracking().AnyAsync(
                    e => e.OrderId == key.OrderId
                         && e.EventType == LicenseKeyRevealPolicy.EventRevealed
                         && e.ActorUserId == userId
                         && e.Description.Contains(keyMarker),
                    cancellationToken);

            if (!revealedBefore)
            {
                if (string.IsNullOrWhiteSpace(request.OtpCode))
                {
                    return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.RevealOtpRequired);
                }

                var verification = await _otp.VerifyAsync(
                    userId, LicenseKeyRevealPolicy.Purpose, keyMarker, request.OtpCode.Trim(), cancellationToken);

                switch (verification)
                {
                    case CustomerActionOtpVerifyStatus.Verified:
                        break;
                    case CustomerActionOtpVerifyStatus.Expired:
                        return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.RevealOtpExpired);
                    case CustomerActionOtpVerifyStatus.Locked:
                        return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.RevealOtpLocked);
                    case CustomerActionOtpVerifyStatus.NotRequested:
                        return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.RevealOtpRequired);
                    default:
                        return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.RevealOtpInvalid);
                }
            }
        }

        // Every plaintext reveal is recorded on the order — who, which key, when — without the key itself.
        _commerceDbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            key.OrderId,
            LicenseKeyRevealPolicy.EventRevealed,
            $"The customer revealed license key {key.Id}.",
            userId,
            _currentUserService.DisplayName ?? userId));
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new RevealCustomerLibraryKeyDto(key.LicenseKey));
    }
}

internal sealed class RequestLicenseKeyRevealOtpCommandHandler
    : IRequestHandler<RequestLicenseKeyRevealOtpCommand, Result<RevealOtpChallengeDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerActionOtpService _otp;

    public RequestLicenseKeyRevealOtpCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService,
        ICustomerActionOtpService otp)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
        _otp = otp;
    }

    public async Task<Result<RevealOtpChallengeDto>> Handle(
        RequestLicenseKeyRevealOtpCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<RevealOtpChallengeDto>(CommerceErrors.AuthenticationRequired);
        }

        var userId = _currentUserService.UserId;

        var owns = await (
            from license in _commerceDbContext.OrderLicenseKeys.AsNoTracking()
            join order in _commerceDbContext.Orders.AsNoTracking() on license.OrderId equals order.Id
            where license.Id == request.LibraryItemId && order.UserId == userId
            select license.Id)
            .AnyAsync(cancellationToken);

        if (!owns)
        {
            return Result.Failure<RevealOtpChallengeDto>(CommerceErrors.OrderLicenseKeyNotFound);
        }

        var result = await _otp.IssueAsync(
            userId, LicenseKeyRevealPolicy.Purpose, request.LibraryItemId.ToString(), cancellationToken);

        return result.Status switch
        {
            CustomerActionOtpIssueStatus.Sent => Result.Success(new RevealOtpChallengeDto(
                result.ExpiresAt!.Value, result.ResendAvailableAt!.Value, result.MaskedEmail ?? "***")),
            CustomerActionOtpIssueStatus.CooldownActive => Result.Failure<RevealOtpChallengeDto>(CommerceErrors.RevealOtpCooldown),
            CustomerActionOtpIssueStatus.Locked => Result.Failure<RevealOtpChallengeDto>(CommerceErrors.RevealOtpLocked),
            CustomerActionOtpIssueStatus.DeliveryFailed => Result.Failure<RevealOtpChallengeDto>(CommerceErrors.RevealOtpDeliveryFailed),
            _ => Result.Failure<RevealOtpChallengeDto>(CommerceErrors.AuthenticationRequired),
        };
    }
}
