using System.Security.Cryptography;
using HAMBOX.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HAMBOX.Infrastructure.Persistence.Conversions;

/// <summary>
/// Transparently encrypts a string column at rest via <see cref="ICodeProtector"/>. EF Core never
/// invokes the conversion for null values, so this converter is safe to apply to nullable columns too.
/// Unlike the one-time backfill migrators (which fail the whole startup if a row can't be read), this
/// converter runs on every ordinary query, so a single row encrypted under a rotated/missing key must
/// degrade to a placeholder instead of throwing and taking down the entire result set.
/// </summary>
public sealed class EncryptedStringConverter(ICodeProtector protector)
    : ValueConverter<string, string>(v => protector.Protect(v), v => SafeUnprotect(protector, v))
{
    private const string UnreadablePlaceholder = "[unreadable]";

    private static string SafeUnprotect(ICodeProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (CryptographicException)
        {
            return ProtectedValueFormat.LooksAlreadyProtected(value) ? UnreadablePlaceholder : value;
        }
    }
}
