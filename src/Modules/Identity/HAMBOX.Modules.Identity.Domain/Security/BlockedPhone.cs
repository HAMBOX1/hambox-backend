namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// Blocks a phone number. Numbers are compared by digits only, and — because the same line is written as
/// <c>01001234567</c>, <c>+201001234567</c> or <c>00201001234567</c> — by their last
/// <see cref="SignificantDigits"/> digits when both sides are at least that long.
/// </summary>
public sealed class BlockedPhone : BlockListEntry
{
    /// <summary>How many trailing digits identify a line regardless of country-code / trunk-prefix formatting.</summary>
    public const int SignificantDigits = 9;

    private BlockedPhone()
    {
    }

    private BlockedPhone(Guid id, string pattern, string reason, string? notes, DateTimeOffset? expiresOnUtc)
        : base(id, reason, notes, expiresOnUtc)
    {
        Pattern = pattern;
    }

    /// <summary>Gets the blocked number, normalized to digits only.</summary>
    public string Pattern { get; private set; } = string.Empty;

    /// <summary>Strips everything but digits and any leading international "00" prefix.</summary>
    public static string Normalize(string phone)
    {
        ArgumentNullException.ThrowIfNull(phone);

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.StartsWith("00", StringComparison.Ordinal) ? digits[2..] : digits;
    }

    /// <summary>Whether <paramref name="candidate"/> (already normalized) is the number <paramref name="pattern"/> blocks.</summary>
    public static bool Matches(string pattern, string candidate)
    {
        if (pattern.Length == 0 || candidate.Length == 0)
        {
            return false;
        }

        if (pattern == candidate)
        {
            return true;
        }

        return pattern.Length >= SignificantDigits
            && candidate.Length >= SignificantDigits
            && pattern[^SignificantDigits..] == candidate[^SignificantDigits..];
    }

    public static BlockedPhone Create(string phone, string reason, string? notes = null, DateTimeOffset? expiresOnUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        var normalized = Normalize(phone);
        if (normalized.Length < 6)
        {
            throw new ArgumentException("A phone number needs at least 6 digits.", nameof(phone));
        }

        return new BlockedPhone(Guid.NewGuid(), normalized, reason, notes, expiresOnUtc);
    }
}
