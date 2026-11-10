namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Answers "does the current admin hold this permission?" from the caller's access token. Endpoint policies already
/// gate whole routes; this is for the cases where one route serves several audiences and a single field (a cost
/// price, say) must only reach staff who hold a specific permission. Anonymous callers, customer-context tokens and
/// background jobs never hold any permission.
/// </summary>
public interface ICurrentUserPermissions
{
    bool Has(string permission);
}
