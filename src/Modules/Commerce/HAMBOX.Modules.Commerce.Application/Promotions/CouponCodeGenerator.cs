using System.Security.Cryptography;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

/// <summary>
/// Generates random coupon codes for bulk creation.
/// </summary>
public static class CouponCodeGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 10, string? prefix = null)
    {
        if (length < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Chars[RandomNumberGenerator.GetInt32(Chars.Length)];
        }

        var code = new string(buffer);
        return prefix is null ? code : $"{prefix.Trim().ToUpperInvariant()}{code}";
    }
}
