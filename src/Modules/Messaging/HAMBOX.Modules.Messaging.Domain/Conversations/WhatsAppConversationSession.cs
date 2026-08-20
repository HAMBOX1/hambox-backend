using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Messaging.Domain.Conversations;

/// <summary>
/// Menu-navigation state for one WhatsApp phone number. One row per phone number — the phone number
/// is the only identity a webhook message carries, so it is the natural key. Deliberately holds no
/// password/credential material: linking to a HAMBOX customer (<see cref="CustomerUserId"/>) happens
/// through a short-lived email verification code (<see cref="PendingVerificationCodeHash"/>), never by
/// trusting the phone number itself as proof of identity.
/// </summary>
public sealed class WhatsAppConversationSession : Entity
{
    private WhatsAppConversationSession()
    {
    }

    private WhatsAppConversationSession(Guid id, string phoneNumber, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        PhoneNumber = phoneNumber;
        CurrentMenu = WhatsAppMenuState.LanguageSelect;
        LanguageCode = "en";
        ExpiresOnUtc = expiresOnUtc;
    }

    public string PhoneNumber { get; private set; } = string.Empty;

    public string CurrentMenu { get; private set; } = WhatsAppMenuState.Main;

    public Guid? SelectedCategoryId { get; private set; }

    public Guid? SelectedProductId { get; private set; }

    public Guid? SelectedVariantId { get; private set; }

    /// <summary>Small free-form JSON blob for transient per-state data (search term, page number,
    /// a not-yet-submitted support message, ...) that doesn't warrant its own column.</summary>
    public string? ContextJson { get; private set; }

    public string LanguageCode { get; private set; } = "en";

    /// <summary>Set only after a successful email-code verification. Null means the phone number is
    /// still unlinked/anonymous — the bot engine gates orders/alerts/support on this being set.</summary>
    public string? CustomerUserId { get; private set; }

    public string? PendingVerificationEmail { get; private set; }

    public string? PendingVerificationCodeHash { get; private set; }

    public DateTimeOffset? PendingVerificationExpiresOnUtc { get; private set; }

    public int PendingVerificationAttempts { get; private set; }

    public DateTimeOffset ExpiresOnUtc { get; private set; }

    public static WhatsAppConversationSession CreateNew(string phoneNumber, TimeSpan slidingWindow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        return new WhatsAppConversationSession(Guid.NewGuid(), phoneNumber.Trim(), DateTimeOffset.UtcNow.Add(slidingWindow));
    }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresOnUtc;

    /// <summary>Extends the sliding-expiry window on every inbound message. Called before any other
    /// mutation so a message that also happens to reset the session still gets a fresh window.</summary>
    public void Touch(TimeSpan slidingWindow, DateTimeOffset nowUtc) => ExpiresOnUtc = nowUtc.Add(slidingWindow);

    public void SetMenu(string menu) => CurrentMenu = menu;

    public void SetSelectedCategory(Guid? categoryId) => SelectedCategoryId = categoryId;

    public void SetSelectedProduct(Guid? productId) => SelectedProductId = productId;

    public void SetSelectedVariant(Guid? variantId) => SelectedVariantId = variantId;

    public void SetContext(string? contextJson) => ContextJson = contextJson;

    public void SetLanguage(string languageCode) => LanguageCode = languageCode;

    /// <summary>Resets navigation back to the main menu — used both for the "0/Back to main" action
    /// and for a session that came back after expiring. Keeps <see cref="LanguageCode"/> and the
    /// verified <see cref="CustomerUserId"/> link (if any); those aren't navigation state.</summary>
    public void ResetToMain()
    {
        CurrentMenu = WhatsAppMenuState.Main;
        SelectedCategoryId = null;
        SelectedProductId = null;
        SelectedVariantId = null;
        ContextJson = null;
    }

    public void BeginVerification(string email, string codeHash, DateTimeOffset expiresOnUtc)
    {
        PendingVerificationEmail = email;
        PendingVerificationCodeHash = codeHash;
        PendingVerificationExpiresOnUtc = expiresOnUtc;
        PendingVerificationAttempts = 0;
    }

    public void RegisterFailedVerificationAttempt() => PendingVerificationAttempts++;

    public void ClearVerification()
    {
        PendingVerificationEmail = null;
        PendingVerificationCodeHash = null;
        PendingVerificationExpiresOnUtc = null;
        PendingVerificationAttempts = 0;
    }

    public void LinkToCustomer(string customerUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerUserId);
        CustomerUserId = customerUserId;
        ClearVerification();
    }

    /// <summary>Drops the verified account link — called specifically when a session is found expired,
    /// so a long-idle phone number must re-prove ownership (email + code) before Orders/Alerts/Support
    /// become reachable again, rather than the link persisting on this phone number forever. Deliberately
    /// separate from <see cref="ResetToMain"/>, which is also used for ordinary "0/Back to main"
    /// navigation and must not sign the user out just because they went back a menu.</summary>
    public void Unlink() => CustomerUserId = null;

    public bool IsLinked => CustomerUserId is not null;
}
