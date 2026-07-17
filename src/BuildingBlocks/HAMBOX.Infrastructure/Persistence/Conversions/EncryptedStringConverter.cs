using HAMBOX.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HAMBOX.Infrastructure.Persistence.Conversions;

/// <summary>
/// Transparently encrypts a string column at rest via <see cref="ICodeProtector"/>. EF Core never
/// invokes the conversion for null values, so this converter is safe to apply to nullable columns too.
/// </summary>
public sealed class EncryptedStringConverter(ICodeProtector protector)
    : ValueConverter<string, string>(v => protector.Protect(v), v => protector.Unprotect(v))
{
}
