using HAMBOX.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// Reads the <c>Idempotency-Key</c> header from the current HTTP request.
/// </summary>
public sealed class IdempotencyKeyAccessor(IHttpContextAccessor httpContextAccessor) : IIdempotencyKeyAccessor
{
    private const string HeaderName = "Idempotency-Key";

    /// <inheritdoc />
    public string? GetIdempotencyKey()
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
