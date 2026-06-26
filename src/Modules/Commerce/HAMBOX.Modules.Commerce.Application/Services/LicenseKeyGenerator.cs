using System.Security.Cryptography;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Generates license key strings for digital product delivery.
/// </summary>
internal static class LicenseKeyGenerator
{
    private const string KeyChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        static string Segment()
        {
            Span<char> segment = stackalloc char[4];
            for (var i = 0; i < segment.Length; i++)
            {
                segment[i] = KeyChars[RandomNumberGenerator.GetInt32(KeyChars.Length)];
            }

            return new string(segment);
        }

        return $"{Segment()}-{Segment()}-{Segment()}-{Segment()}";
    }
}
