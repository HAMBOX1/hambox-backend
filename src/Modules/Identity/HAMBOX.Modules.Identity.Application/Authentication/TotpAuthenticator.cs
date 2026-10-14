using System.Security.Cryptography;
using System.Text;

namespace HAMBOX.Modules.Identity.Application.Authentication;

/// <summary>
/// RFC 6238 time-based one-time passwords (HMAC-SHA1, 30-second step, 6 digits — the profile every
/// authenticator app implements) plus the RFC 4648 base-32 encoding used for the shared secret. Pure and
/// dependency-free so it can be unit-tested against the RFC's published vectors.
/// </summary>
public static class TotpAuthenticator
{
    public const int PeriodSeconds = 30;
    public const int Digits = 6;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>A fresh 160-bit secret (the size RFC 4226 recommends), base-32 encoded.</summary>
    public static string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public static long TimeStep(DateTimeOffset at, int periodSeconds = PeriodSeconds) =>
        at.ToUnixTimeSeconds() / periodSeconds;

    public static string ComputeCode(string base32Secret, long timeStep, int digits = Digits)
    {
        var key = Base32Decode(base32Secret);
        Span<byte> counter = stackalloc byte[8];
        var value = timeStep;
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(value & 0xFF);
            value >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                   | (hash[offset + 1] << 16)
                   | (hash[offset + 2] << 8)
                   | hash[offset + 3];

        var modulo = 1;
        for (var i = 0; i < digits; i++)
        {
            modulo *= 10;
        }

        return (binary % modulo).ToString().PadLeft(digits, '0');
    }

    /// <summary>
    /// Checks <paramref name="code"/> against the current step and <paramref name="window"/> steps either side
    /// (clock drift). Returns the matching step so the caller can refuse to accept the same step twice, or
    /// <see langword="null"/> when nothing matches or the match is not newer than <paramref name="lastUsedStep"/>.
    /// </summary>
    public static long? Verify(string base32Secret, string code, DateTimeOffset now, long lastUsedStep, int window = 1)
    {
        var candidate = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (candidate.Length != Digits)
        {
            return null;
        }

        var current = TimeStep(now);
        long? matched = null;

        // No early exit: every step is compared so timing does not reveal which one (if any) matched.
        for (var step = current - window; step <= current + window; step++)
        {
            var expected = ComputeCode(base32Secret, step);
            var equal = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(candidate));

            if (equal && step > lastUsedStep && (matched is null || step > matched))
            {
                matched = step;
            }
        }

        return matched;
    }

    /// <summary>The otpauth:// URI authenticator apps import (usually rendered as a QR code).</summary>
    public static string BuildOtpAuthUri(string issuer, string accountName, string base32Secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}"
             + $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
    }

    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return output.ToString();
    }

    public static byte[] Base32Decode(string value)
    {
        var cleaned = value.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        var output = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in cleaned)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException("Invalid base-32 character in secret.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }

    /// <summary>Ten-ish human-friendly recovery codes: 10 characters from an unambiguous alphabet, "XXXXX-XXXXX".</summary>
    public static string GenerateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[11];
        for (var i = 0; i < 11; i++)
        {
            chars[i] = i == 5 ? '-' : alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(chars);
    }
}
