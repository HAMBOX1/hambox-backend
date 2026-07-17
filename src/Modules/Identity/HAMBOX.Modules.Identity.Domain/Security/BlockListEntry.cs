using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// Common shape shared by every Security Center blocklist entry (<see cref="BlockedEmail"/>,
/// <see cref="BlockedIp"/>, <see cref="CountryRestriction"/>, <see cref="BlockedDevice"/>):
/// a reason, optional notes, an optional expiration, and standard audit/soft-delete tracking.
/// Removing an entry is a soft delete (<see cref="ISoftDeletable"/>), so there is no separate
/// "is active" flag — an entry is active precisely when it is not deleted and not expired.
/// </summary>
public abstract class BlockListEntry : Entity, IAuditable, ISoftDeletable
{
    protected BlockListEntry()
    {
    }

    protected BlockListEntry(Guid id, string reason, string? notes, DateTimeOffset? expiresOnUtc)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Reason = reason;
        Notes = notes;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>
    /// Gets the administrator-supplied reason this entry was added.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the administrator's free-text notes, if any.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, this entry expires. Null means the entry is permanent
    /// and remains in effect until an administrator removes it.
    /// </summary>
    public DateTimeOffset? ExpiresOnUtc { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this entry has no expiration.
    /// </summary>
    public bool IsPermanent => ExpiresOnUtc is null;

    /// <summary>
    /// Gets a value indicating whether this entry is currently in effect (not expired).
    /// Deleted entries are excluded by the global soft-delete query filter before this is evaluated.
    /// </summary>
    public bool IsCurrentlyActive => ExpiresOnUtc is null || ExpiresOnUtc > DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    /// <summary>
    /// Updates the reason, notes, and expiration of this entry. Used by subclasses that support
    /// editing an existing entry in place (e.g. <see cref="CountryRestriction"/>) rather than
    /// only create/soft-delete.
    /// </summary>
    protected void SetReason(string reason, string? notes, DateTimeOffset? expiresOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Reason = reason;
        Notes = notes;
        ExpiresOnUtc = expiresOnUtc;
    }
}
