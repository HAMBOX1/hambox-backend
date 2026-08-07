using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.IntegrationTests.Referrals;

/// <summary>
/// A settings provider backed by a single mutable <see cref="Referral"/> payload, the only category
/// the referral engine reads. Every other category throws — nothing under test should touch them.
/// </summary>
internal sealed class FakeReferralPlatformSettingsProvider : IPlatformSettingsProvider
{
    public ReferralSettingsPayload Referral { get; set; } = new(
        Enabled: true,
        PointsPerReferral: 100,
        PointValueUsd: 0.10m,
        RewardExpiryDays: 30);

    public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (categoryKey == PlatformSettingsCategoryKeys.Referral && typeof(T) == typeof(ReferralSettingsPayload))
        {
            return Task.FromResult((T)(object)Referral);
        }

        throw new NotSupportedException($"FakeReferralPlatformSettingsProvider has no fake for category '{categoryKey}'.");
    }

    public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> VerifyMaintenanceBypassPasswordAsync(string password, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void InvalidateCache(string? categoryKey = null)
    {
    }
}

/// <summary>Records every notification the referral engine tries to send, without actually sending anything.</summary>
internal sealed class FakeCommunicationService : ICommunicationService
{
    public List<CommunicationRequest> Sent { get; } = [];

    public Task<Result> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        return Task.FromResult(Result.Success());
    }
}

/// <summary>Returns a fixed membership snapshot — <see cref="MembershipSnapshot.None"/> (1x referral multiplier) unless overridden.</summary>
internal sealed class FakeMembershipEngine : IMembershipEngine
{
    public MembershipSnapshot Snapshot { get; set; } = MembershipSnapshot.None;

    public Task<MembershipSnapshot> ResolveAsync(string? userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshot);

    public Task ProcessExpirationsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>No-op protector — tests never touch the encrypted fields it would guard.</summary>
internal sealed class FakeCodeProtector : ICodeProtector
{
    public string Protect(string plainText) => plainText;

    public string Unprotect(string cipherText) => cipherText;
}
