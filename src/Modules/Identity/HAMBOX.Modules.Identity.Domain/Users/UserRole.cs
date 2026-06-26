using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Users;

/// <summary>
/// Represents the relationship between a user and a role.
/// </summary>
public sealed class UserRole : Entity
{
    private UserRole()
    {
    }

    private UserRole(Guid id, Guid userId, Guid roleId)
        : base(id)
    {
        UserId = userId;
        RoleId = roleId;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the role identifier.
    /// </summary>
    public Guid RoleId { get; private set; }

    /// <summary>
    /// Creates a new user-role mapping.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>A new <see cref="UserRole"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when any identifier is empty.</exception>
    public static UserRole Create(Guid userId, Guid roleId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        return new UserRole(Guid.NewGuid(), userId, roleId);
    }
}
