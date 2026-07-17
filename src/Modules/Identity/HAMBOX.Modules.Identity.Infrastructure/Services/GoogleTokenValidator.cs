using Google.Apis.Auth;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Validates Google ID tokens using Google's official token verification library.
/// </summary>
internal sealed class GoogleTokenValidator(IOptions<GoogleAuthSettings> googleAuthSettings) : IGoogleTokenValidator
{
    public async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = googleAuthSettings.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleTokenPayload(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.GivenName,
                payload.FamilyName);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
