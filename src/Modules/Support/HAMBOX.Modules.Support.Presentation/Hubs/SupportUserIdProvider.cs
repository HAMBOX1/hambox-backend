using Microsoft.AspNetCore.SignalR;

namespace HAMBOX.Modules.Support.Presentation.Hubs;

/// <summary>
/// SignalR's <see cref="IUserIdProvider"/> default reads <c>ClaimTypes.NameIdentifier</c>, but
/// this API's JWTs (<c>MapInboundClaims = false</c>) carry the raw <c>sub</c> claim instead —
/// the same claim <c>CurrentUserService</c> reads. Without this, <c>Context.UserIdentifier</c>
/// (and therefore every <c>user-{userId}</c> group) would always be null.
/// </summary>
public sealed class SupportUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst("sub")?.Value ?? connection.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
