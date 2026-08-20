using System.Security.Cryptography;
using System.Text;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Messaging.Application.Services;

public enum WhatsAppLinkStartResult
{
    CodeSent,
    NoAccountFound,
}

public enum WhatsAppLinkConfirmResult
{
    Linked,
    CodeMismatch,
    Expired,
    TooManyAttempts,
    NoPendingVerification,
}

/// <summary>
/// Gates "My Orders" / "My Alerts" / "Support" behind proof that the WhatsApp sender actually owns a
/// HAMBOX account — deliberately the smallest thing that is still real proof: a one-time numeric code
/// emailed to the account's registered address (reusing Identity's <see cref="IOtpCodeGenerator"/> and
/// the existing <see cref="ICommunicationService"/> pipeline, not a new OTP subsystem). The phone number
/// itself is never treated as identity; only <see cref="WhatsAppConversationSession.CustomerUserId"/>
/// being set (only ever set here) unlocks another user's data.
/// </summary>
public sealed class WhatsAppLinkVerificationService(
    IIdentityDbContext identityDb,
    IMessagingDbContext messagingDb,
    IOtpCodeGenerator codeGenerator,
    ICommunicationService communicationService)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const int CodeLength = 6;

    // ponytail: flat max-attempts cap, no per-IP/exponential backoff — Identity's AdminLoginChallenge
    // has that machinery for the higher-value admin login surface; add here if WhatsApp-link abuse
    // is ever observed.
    private const int MaxAttempts = 5;

    public async Task<WhatsAppLinkStartResult> StartVerificationAsync(
        WhatsAppConversationSession session, string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await identityDb.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return WhatsAppLinkStartResult.NoAccountFound;
        }

        var code = codeGenerator.GenerateNumericCode(CodeLength);
        session.BeginVerification(user.Email, Hash(code), DateTimeOffset.UtcNow.Add(CodeLifetime));
        await messagingDb.SaveChangesAsync(cancellationToken);

        await communicationService.SendAsync(
            new CommunicationRequest(
                user.Id.ToString(),
                MessagingTemplateKeys.WhatsAppLinkVerificationCode,
                CommunicationCategory.Security,
                new Dictionary<string, string>
                {
                    ["Code"] = code,
                    ["ExpiresInMinutes"] = CodeLifetime.TotalMinutes.ToString("0"),
                },
                CommunicationPriority.High,
                ChannelOverride: ["Email"]),
            cancellationToken);

        return WhatsAppLinkStartResult.CodeSent;
    }

    public async Task<WhatsAppLinkConfirmResult> ConfirmAsync(
        WhatsAppConversationSession session, string enteredCode, CancellationToken cancellationToken)
    {
        if (session.PendingVerificationCodeHash is null || session.PendingVerificationExpiresOnUtc is null)
        {
            return WhatsAppLinkConfirmResult.NoPendingVerification;
        }

        if (DateTimeOffset.UtcNow >= session.PendingVerificationExpiresOnUtc)
        {
            session.ClearVerification();
            await messagingDb.SaveChangesAsync(cancellationToken);
            return WhatsAppLinkConfirmResult.Expired;
        }

        if (session.PendingVerificationAttempts >= MaxAttempts)
        {
            session.ClearVerification();
            await messagingDb.SaveChangesAsync(cancellationToken);
            return WhatsAppLinkConfirmResult.TooManyAttempts;
        }

        if (!FixedTimeEquals(Hash(enteredCode.Trim()), session.PendingVerificationCodeHash))
        {
            session.RegisterFailedVerificationAttempt();
            await messagingDb.SaveChangesAsync(cancellationToken);
            return WhatsAppLinkConfirmResult.CodeMismatch;
        }

        var normalizedEmail = session.PendingVerificationEmail!.Trim().ToUpperInvariant();
        var user = await identityDb.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            session.ClearVerification();
            await messagingDb.SaveChangesAsync(cancellationToken);
            return WhatsAppLinkConfirmResult.NoPendingVerification;
        }

        session.LinkToCustomer(user.Id.ToString());
        await messagingDb.SaveChangesAsync(cancellationToken);
        return WhatsAppLinkConfirmResult.Linked;
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
