namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Resolves who should be emailed when a High/Critical security event occurs: every Owner, plus
/// anyone holding <see cref="Authorization.PermissionConstants.Security.ManageAlerts"/> or
/// <see cref="Authorization.PermissionConstants.Security.View"/>.
/// </summary>
public interface ISecurityAlertRecipientResolver
{
    Task<IReadOnlyCollection<Guid>> GetRecipientUserIdsAsync(CancellationToken cancellationToken = default);
}
