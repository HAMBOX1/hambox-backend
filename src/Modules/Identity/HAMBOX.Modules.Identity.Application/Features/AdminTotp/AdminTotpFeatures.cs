using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authentication;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.AdminTotp;

public sealed record AdminTotpStatusDto(bool IsEnabled, bool EnrollmentPending, int RecoveryCodesRemaining);

public sealed record AdminTotpEnrollmentDto(string Secret, string OtpAuthUri, string Issuer, string AccountName);

public sealed record AdminTotpRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

public sealed record GetAdminTotpStatusQuery : IRequest<Result<AdminTotpStatusDto>>;

public sealed record BeginAdminTotpEnrollmentCommand : IRequest<Result<AdminTotpEnrollmentDto>>;

public sealed record ConfirmAdminTotpEnrollmentCommand(string Code, string? IpAddress) : IRequest<Result<AdminTotpRecoveryCodesDto>>;

/// <param name="Password">The admin's own password — a stolen session alone must not be able to remove a factor.</param>
/// <param name="Code">A current authenticator code or one unused recovery code.</param>
public sealed record DisableAdminTotpCommand(string Password, string Code, string? IpAddress) : IRequest<Result>;

public sealed record RegenerateAdminRecoveryCodesCommand(string Password, string Code, string? IpAddress)
    : IRequest<Result<AdminTotpRecoveryCodesDto>>;

/// <summary>Owner-only escape hatch for an admin who lost both their authenticator and their recovery codes.</summary>
public sealed record ResetAdminTotpCommand(Guid TargetUserId, string? IpAddress) : IRequest<Result>;

internal sealed class ConfirmAdminTotpEnrollmentCommandValidator : AbstractValidator<ConfirmAdminTotpEnrollmentCommand>
{
    public ConfirmAdminTotpEnrollmentCommandValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
}

internal sealed class DisableAdminTotpCommandValidator : AbstractValidator<DisableAdminTotpCommand>
{
    public DisableAdminTotpCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

internal sealed class RegenerateAdminRecoveryCodesCommandValidator : AbstractValidator<RegenerateAdminRecoveryCodesCommand>
{
    public RegenerateAdminRecoveryCodesCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

/// <summary>What every authenticated admin-TOTP handler needs: who is asking, and proof they may.</summary>
internal static class AdminTotpSupport
{
    public const int RecoveryCodeCount = 10;

    public static async Task<(ApplicationUserRef? User, Error? Failure)> ResolveAdminAsync(
        IIdentityDbContext db,
        ICurrentUserService currentUser,
        IAdminAccessResolver adminAccess,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            return (null, IdentityErrors.AuthenticationRequired);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, IdentityErrors.UserNotFound);
        }

        if (!await adminAccess.HasAdminPortalAccessAsync(userId, cancellationToken))
        {
            return (null, IdentityErrors.AdminPortalAccessDenied);
        }

        return (new ApplicationUserRef(user.Id, user.Email, user.PasswordHash), null);
    }

    public sealed record ApplicationUserRef(Guid Id, string Email, string PasswordHash);

    /// <summary>
    /// Accepts either a current authenticator code (replay-protected) or one unused recovery code, and spends
    /// whichever matched. Returns false without changing anything when neither is valid.
    /// </summary>
    public static async Task<bool> ConsumeSecondFactorAsync(
        IIdentityDbContext db,
        ICodeProtector protector,
        AdminTotpCredential credential,
        string code,
        CancellationToken cancellationToken)
    {
        var secret = protector.Unprotect(credential.ProtectedSecret);
        var step = TotpAuthenticator.Verify(secret, code, DateTimeOffset.UtcNow, credential.LastUsedTimeStep);
        if (step is not null)
        {
            credential.RecordUse(step.Value);
            return true;
        }

        var hash = AdminRecoveryCode.ComputeHash(code);
        var recovery = await db.AdminRecoveryCodes
            .FirstOrDefaultAsync(r => r.UserId == credential.UserId && r.CodeHash == hash && r.UsedOnUtc == null, cancellationToken);
        if (recovery is null)
        {
            return false;
        }

        recovery.MarkUsed();
        return true;
    }

    public static async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(
        IIdentityDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.AdminRecoveryCodes.Where(r => r.UserId == userId).ToListAsync(cancellationToken);
        db.AdminRecoveryCodes.RemoveRange(existing);

        var plaintext = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var code = TotpAuthenticator.GenerateRecoveryCode();
            plaintext.Add(code);
            db.AdminRecoveryCodes.Add(AdminRecoveryCode.Create(userId, code));
        }

        return plaintext;
    }
}

internal sealed class GetAdminTotpStatusQueryHandler(
    IIdentityDbContext db,
    ICurrentUserService currentUser,
    IAdminAccessResolver adminAccess) : IRequestHandler<GetAdminTotpStatusQuery, Result<AdminTotpStatusDto>>
{
    public async Task<Result<AdminTotpStatusDto>> Handle(GetAdminTotpStatusQuery request, CancellationToken cancellationToken)
    {
        var (user, failure) = await AdminTotpSupport.ResolveAdminAsync(db, currentUser, adminAccess, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AdminTotpStatusDto>(failure!);
        }

        var credential = await db.AdminTotpCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        var remaining = credential is { IsConfirmed: true }
            ? await db.AdminRecoveryCodes.CountAsync(r => r.UserId == user.Id && r.UsedOnUtc == null, cancellationToken)
            : 0;

        return Result.Success(new AdminTotpStatusDto(
            credential is { IsConfirmed: true },
            credential is { IsConfirmed: false },
            remaining));
    }
}

internal sealed class BeginAdminTotpEnrollmentCommandHandler(
    IIdentityDbContext db,
    ICurrentUserService currentUser,
    IAdminAccessResolver adminAccess,
    ICodeProtector protector,
    IPlatformSettingsProvider platformSettings) : IRequestHandler<BeginAdminTotpEnrollmentCommand, Result<AdminTotpEnrollmentDto>>
{
    public async Task<Result<AdminTotpEnrollmentDto>> Handle(
        BeginAdminTotpEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var (user, failure) = await AdminTotpSupport.ResolveAdminAsync(db, currentUser, adminAccess, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AdminTotpEnrollmentDto>(failure!);
        }

        var credential = await db.AdminTotpCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        if (credential is { IsConfirmed: true })
        {
            return Result.Failure<AdminTotpEnrollmentDto>(IdentityErrors.TotpAlreadyEnabled);
        }

        var secret = TotpAuthenticator.GenerateSecret();
        var protectedSecret = protector.Protect(secret);

        if (credential is null)
        {
            db.AdminTotpCredentials.Add(AdminTotpCredential.Begin(user.Id, protectedSecret));
        }
        else
        {
            credential.ReplaceSecret(protectedSecret);
        }

        await db.SaveChangesAsync(cancellationToken);

        var totp = await platformSettings.GetAsync<TotpSettingsPayload>(PlatformSettingsCategoryKeys.Totp, cancellationToken);
        var issuer = string.IsNullOrWhiteSpace(totp.Issuer) ? "HAMBOX Admin" : totp.Issuer;

        return Result.Success(new AdminTotpEnrollmentDto(
            secret,
            TotpAuthenticator.BuildOtpAuthUri(issuer, user.Email, secret),
            issuer,
            user.Email));
    }
}

internal sealed class ConfirmAdminTotpEnrollmentCommandHandler(
    IIdentityDbContext db,
    ICurrentUserService currentUser,
    IAdminAccessResolver adminAccess,
    ICodeProtector protector) : IRequestHandler<ConfirmAdminTotpEnrollmentCommand, Result<AdminTotpRecoveryCodesDto>>
{
    public async Task<Result<AdminTotpRecoveryCodesDto>> Handle(
        ConfirmAdminTotpEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var (user, failure) = await AdminTotpSupport.ResolveAdminAsync(db, currentUser, adminAccess, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(failure!);
        }

        var credential = await db.AdminTotpCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        if (credential is null)
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(IdentityErrors.TotpEnrollmentNotStarted);
        }

        if (credential.IsConfirmed)
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(IdentityErrors.TotpAlreadyEnabled);
        }

        var secret = protector.Unprotect(credential.ProtectedSecret);
        var step = TotpAuthenticator.Verify(secret, request.Code, DateTimeOffset.UtcNow, credential.LastUsedTimeStep);
        if (step is null)
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(IdentityErrors.TotpCodeInvalid);
        }

        credential.Confirm(step.Value);
        var codes = await AdminTotpSupport.ReplaceRecoveryCodesAsync(db, user.Id, cancellationToken);
        db.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionTotpEnabled, request.IpAddress ?? "unknown", user.Id, details: "Authenticator app enrolled."));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdminTotpRecoveryCodesDto(codes));
    }
}

internal sealed class DisableAdminTotpCommandHandler(
    IIdentityDbContext db,
    ICurrentUserService currentUser,
    IAdminAccessResolver adminAccess,
    IPasswordHasher passwordHasher,
    ICodeProtector protector) : IRequestHandler<DisableAdminTotpCommand, Result>
{
    public async Task<Result> Handle(DisableAdminTotpCommand request, CancellationToken cancellationToken)
    {
        var (user, failure) = await AdminTotpSupport.ResolveAdminAsync(db, currentUser, adminAccess, cancellationToken);
        if (user is null)
        {
            return Result.Failure(failure!);
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            return Result.Failure(IdentityErrors.InvalidCurrentPassword);
        }

        var credential = await db.AdminTotpCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        if (credential is null || !credential.IsConfirmed)
        {
            return Result.Failure(IdentityErrors.TotpNotEnabled);
        }

        if (!await AdminTotpSupport.ConsumeSecondFactorAsync(db, protector, credential, request.Code, cancellationToken))
        {
            return Result.Failure(IdentityErrors.TotpCodeInvalid);
        }

        db.AdminTotpCredentials.Remove(credential);
        db.AdminRecoveryCodes.RemoveRange(await db.AdminRecoveryCodes.Where(r => r.UserId == user.Id).ToListAsync(cancellationToken));
        db.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionTotpDisabled, request.IpAddress ?? "unknown", user.Id, details: "Authenticator app removed by the admin."));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class RegenerateAdminRecoveryCodesCommandHandler(
    IIdentityDbContext db,
    ICurrentUserService currentUser,
    IAdminAccessResolver adminAccess,
    IPasswordHasher passwordHasher,
    ICodeProtector protector) : IRequestHandler<RegenerateAdminRecoveryCodesCommand, Result<AdminTotpRecoveryCodesDto>>
{
    public async Task<Result<AdminTotpRecoveryCodesDto>> Handle(
        RegenerateAdminRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        var (user, failure) = await AdminTotpSupport.ResolveAdminAsync(db, currentUser, adminAccess, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(failure!);
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(IdentityErrors.InvalidCurrentPassword);
        }

        var credential = await db.AdminTotpCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        if (credential is null || !credential.IsConfirmed)
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(IdentityErrors.TotpNotEnabled);
        }

        if (!await AdminTotpSupport.ConsumeSecondFactorAsync(db, protector, credential, request.Code, cancellationToken))
        {
            return Result.Failure<AdminTotpRecoveryCodesDto>(IdentityErrors.TotpCodeInvalid);
        }

        var codes = await AdminTotpSupport.ReplaceRecoveryCodesAsync(db, user.Id, cancellationToken);
        db.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionRecoveryCodesRegenerated, request.IpAddress ?? "unknown", user.Id));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdminTotpRecoveryCodesDto(codes));
    }
}

internal sealed class ResetAdminTotpCommandHandler(
    IIdentityDbContext db,
    ICurrentUserService currentUser,
    IRbacAuthorizationService rbac) : IRequestHandler<ResetAdminTotpCommand, Result>
{
    public async Task<Result> Handle(ResetAdminTotpCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var actorId) || !await rbac.IsOwnerAsync(actorId, cancellationToken))
        {
            return Result.Failure(IdentityErrors.PermissionDenied);
        }

        var credential = await db.AdminTotpCredentials.FirstOrDefaultAsync(c => c.UserId == request.TargetUserId, cancellationToken);
        if (credential is null)
        {
            return Result.Failure(IdentityErrors.TotpNotEnabled);
        }

        db.AdminTotpCredentials.Remove(credential);
        db.AdminRecoveryCodes.RemoveRange(
            await db.AdminRecoveryCodes.Where(r => r.UserId == request.TargetUserId).ToListAsync(cancellationToken));
        db.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionTotpReset, request.IpAddress ?? "unknown", request.TargetUserId,
            details: $"Authenticator removed by owner {actorId}."));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
