namespace HAMBOX.Modules.Messaging.Application.Services;

/// <summary>Masks a phone number down to its last 4 digits before it ever reaches a log — phone numbers
/// are PII and must never appear in cleartext in logs (see Commerce's <c>MsisdnMasker</c> for the same
/// rule applied to Dot payment MSISDNs; duplicated here in miniature rather than shared across modules,
/// since the source type is <c>internal</c> to Commerce.Application).</summary>
public static class WhatsAppPhoneMasker
{
    public static string Mask(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var trimmed = phoneNumber.Trim();
        return trimmed.Length <= 4
            ? new string('*', trimmed.Length)
            : new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}
