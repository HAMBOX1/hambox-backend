namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Issues and validates opaque, claims-free tokens that let a browser through the
/// storefront's maintenance-mode gate without granting any authentication/authorization.
/// </summary>
public interface IMaintenanceBypassTokenIssuer
{
    (string Token, DateTimeOffset ExpiresOnUtc) Issue(TimeSpan validFor);

    bool TryValidate(string token);
}
