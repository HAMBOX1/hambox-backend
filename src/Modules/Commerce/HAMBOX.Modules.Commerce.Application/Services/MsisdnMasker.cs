namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>Masks a mobile number down to its last 4 digits before it's ever persisted or logged.</summary>
internal static class MsisdnMasker
{
    public static string? Mask(string? msisdn)
    {
        if (string.IsNullOrWhiteSpace(msisdn))
        {
            return null;
        }

        var digits = msisdn.Trim();
        return digits.Length <= 4
            ? new string('*', digits.Length)
            : new string('*', digits.Length - 4) + digits[^4..];
    }
}
