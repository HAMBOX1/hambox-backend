using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Represents an in-app notification for a user.
/// </summary>
public sealed class UserNotification : Entity
{
    private UserNotification()
    {
    }

    private UserNotification(
        Guid id,
        string userId,
        string title,
        string body,
        string category,
        string? actionUrl)
        : base(id)
    {
        UserId = userId;
        Title = title;
        Body = body;
        Category = category;
        ActionUrl = actionUrl;
        IsRead = false;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the notification title.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the notification body.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the notification category.
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Gets an optional deep-link for the notification (e.g. library item).
    /// </summary>
    public string? ActionUrl { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the notification has been read.
    /// </summary>
    public bool IsRead { get; private set; }

    /// <summary>
    /// Creates a new user notification.
    /// </summary>
    public static UserNotification Create(
        string userId,
        string title,
        string body,
        string category,
        string? actionUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        return new UserNotification(
            Guid.NewGuid(),
            userId,
            title,
            body,
            category,
            string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim());
    }

    /// <summary>
    /// Marks the notification as read.
    /// </summary>
    public void MarkAsRead()
    {
        IsRead = true;
    }
}
