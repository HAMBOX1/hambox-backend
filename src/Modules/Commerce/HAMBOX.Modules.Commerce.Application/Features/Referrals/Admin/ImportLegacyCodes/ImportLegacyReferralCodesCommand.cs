using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.ImportLegacyCodes;

public sealed record LegacyReferralCodeRow(string Code, string? Email, string? UserId);

public sealed record LegacyCodeIssueDto(int Row, string Code, string Reason);

public sealed record ImportLegacyReferralCodesResultDto(
    int Imported, int AlreadyImported, IReadOnlyList<LegacyCodeIssueDto> Issues);

/// <summary>
/// Carries retired PRIME referral codes over to their owners (LV-03). It is a dry-run-safe, repeatable import: a code that
/// is already mapped to the same owner is skipped, never duplicated or paid twice; a code that collides with a current code
/// or another owner is reported, not overwritten.
/// </summary>
public sealed record ImportLegacyReferralCodesCommand(IReadOnlyList<LegacyReferralCodeRow> Rows)
    : IRequest<Result<ImportLegacyReferralCodesResultDto>>;

internal sealed class ImportLegacyReferralCodesCommandValidator : AbstractValidator<ImportLegacyReferralCodesCommand>
{
    public ImportLegacyReferralCodesCommandValidator()
    {
        RuleFor(x => x.Rows).NotEmpty().Must(r => r.Count <= 5000).WithMessage("Import at most 5,000 codes at a time.");
        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.Code).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9-]+$");
            row.RuleFor(r => r.Email).MaximumLength(256);
            row.RuleFor(r => r.UserId).MaximumLength(450);
        });
    }
}

internal sealed class ImportLegacyReferralCodesCommandHandler(ICommerceDbContext db, IUserDirectory users)
    : IRequestHandler<ImportLegacyReferralCodesCommand, Result<ImportLegacyReferralCodesResultDto>>
{
    public async Task<Result<ImportLegacyReferralCodesResultDto>> Handle(
        ImportLegacyReferralCodesCommand request, CancellationToken cancellationToken)
    {
        var emails = await users.FindUserIdsByEmailAsync(
            request.Rows.Where(r => !string.IsNullOrWhiteSpace(r.Email)).Select(r => r.Email!).ToList(), cancellationToken);
        var explicitIds = await users.GetExistingUserIdsAsync(
            request.Rows.Where(r => !string.IsNullOrWhiteSpace(r.UserId)).Select(r => r.UserId!).ToList(), cancellationToken);

        var codes = request.Rows.Select(r => r.Code.Trim().ToUpperInvariant()).Distinct().ToList();
        var legacy = await db.ReferralLegacyCodes.Where(c => codes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, cancellationToken);
        var current = (await db.ReferralProfiles.AsNoTracking().Where(p => codes.Contains(p.ReferralCode.ToUpper())).Select(p => p.ReferralCode.ToUpper()).ToListAsync(cancellationToken)).ToHashSet();

        var imported = 0;
        var already = 0;
        var issues = new List<LegacyCodeIssueDto>();
        var seen = new Dictionary<string, string>();

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var code = row.Code.Trim().ToUpperInvariant();
            var ownerId = !string.IsNullOrWhiteSpace(row.UserId) && explicitIds.Contains(row.UserId)
                ? row.UserId
                : !string.IsNullOrWhiteSpace(row.Email) && emails.TryGetValue(row.Email.Trim(), out var id) ? id : null;

            if (ownerId is null)
            {
                issues.Add(new LegacyCodeIssueDto(i + 1, code, "No matching customer."));
                continue;
            }

            if (seen.TryGetValue(code, out var firstOwner))
            {
                if (!string.Equals(firstOwner, ownerId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new LegacyCodeIssueDto(i + 1, code, "The same code appears for two different customers in this file."));
                }

                continue;
            }

            seen[code] = ownerId;

            if (legacy.TryGetValue(code, out var existing))
            {
                if (string.Equals(existing.OwnerUserId, ownerId, StringComparison.OrdinalIgnoreCase))
                {
                    already++;
                }
                else
                {
                    issues.Add(new LegacyCodeIssueDto(i + 1, code, "Already imported for a different customer."));
                }

                continue;
            }

            if (current.Contains(code))
            {
                issues.Add(new LegacyCodeIssueDto(i + 1, code, "Collides with a current referral code."));
                continue;
            }

            await ReferralProfileProvisioner.GetOrCreateAsync(db, ownerId, cancellationToken);
            db.ReferralLegacyCodes.Add(ReferralLegacyCode.Create(code, ownerId, "PRIME"));
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new ImportLegacyReferralCodesResultDto(imported, already, issues));
    }
}
